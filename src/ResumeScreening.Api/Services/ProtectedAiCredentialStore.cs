using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ResumeScreening.Api.Services;

public interface IAiCredentialStore
{
    bool IsConfigured { get; }

    string? GetApiKey();

    void SetApiKey(string apiKey);
}

public sealed class ProtectedAiCredentialStore : IAiCredentialStore
{
    private const string ProtectorPurpose = "ResumeScreening.DeepSeekApiKey.v1";
    private readonly object _writeGate = new();
    private readonly IDataProtector _protector;
    private readonly ILogger<ProtectedAiCredentialStore> _logger;
    private readonly string _credentialPath;
    private string? _apiKey;

    public ProtectedAiCredentialStore(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AiOptions> options,
        ILogger<ProtectedAiCredentialStore> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger;

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ResumeScreening");
        Directory.CreateDirectory(dataDirectory);
        _credentialPath = Path.Combine(dataDirectory, "deepseek-api-key.protected");

        if (TryLoadCachedApiKey())
        {
            return;
        }

        var configuredApiKey = options.Value.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredApiKey))
        {
            SetApiKey(configuredApiKey);
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Volatile.Read(ref _apiKey));

    public string? GetApiKey() => Volatile.Read(ref _apiKey);

    public void SetApiKey(string apiKey)
    {
        var normalizedApiKey = apiKey.Trim();
        if (normalizedApiKey.Length == 0)
        {
            throw new ArgumentException("API Key 不能为空。", nameof(apiKey));
        }

        lock (_writeGate)
        {
            var protectedValue = _protector.Protect(normalizedApiKey);
            var temporaryPath = $"{_credentialPath}.{Guid.NewGuid():N}.tmp";

            try
            {
                File.WriteAllText(temporaryPath, protectedValue);
                File.Move(temporaryPath, _credentialPath, overwrite: true);
                Volatile.Write(ref _apiKey, normalizedApiKey);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    private bool TryLoadCachedApiKey()
    {
        if (!File.Exists(_credentialPath))
        {
            return false;
        }

        try
        {
            var protectedValue = File.ReadAllText(_credentialPath);
            var apiKey = _protector.Unprotect(protectedValue).Trim();
            if (apiKey.Length > 0)
            {
                Volatile.Write(ref _apiKey, apiKey);
                return true;
            }
        }
        catch (Exception exception) when (
            exception is CryptographicException
                or IOException
                or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                exception,
                "The cached AI credential could not be loaded. Configure it again from Settings.");
        }

        return false;
    }
}
