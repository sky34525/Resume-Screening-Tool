using System.ComponentModel;
using System.Diagnostics;

namespace ResumeScreening_Desktop.Services;

internal static class PythonPdfByteReader
{
    public static async Task<byte[]> ReadAsync(string sourcePath, long maxFileSizeBytes)
    {
        var helperPath = Path.Combine(AppContext.BaseDirectory, "Services", "read_local_pdf.py");
        if (!File.Exists(helperPath))
        {
            throw new InvalidDataException("本地 PDF 读取组件缺失");
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"resume-import-{Guid.NewGuid():N}.pdf");
        var failures = new List<string>();

        try
        {
            foreach (var interpreter in new[]
                     {
                         new PythonInterpreter("py", "-3"),
                         new PythonInterpreter("python", null)
                     })
            {
                try
                {
                    var result = await RunHelperAsync(interpreter, helperPath, sourcePath, outputPath, maxFileSizeBytes);
                    if (result.ExitCode == 0)
                    {
                        var bytes = await File.ReadAllBytesAsync(outputPath);
                        if (bytes.LongLength > maxFileSizeBytes)
                        {
                            throw new InvalidDataException("文件超过 25 MB 上限");
                        }

                        return bytes;
                    }

                    failures.Add(result.Error);
                }
                catch (Win32Exception exception)
                {
                    failures.Add(exception.Message);
                }
            }

            throw new InvalidDataException(
                failures.Any(message => message.Contains("not a PDF", StringComparison.OrdinalIgnoreCase))
                    ? "文件头不是有效的 PDF"
                    : "本地 PDF 读取失败；请确认已安装 Python 3");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static async Task<ProcessResult> RunHelperAsync(
        PythonInterpreter interpreter,
        string helperPath,
        string sourcePath,
        string outputPath,
        long maxFileSizeBytes)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = interpreter.Command,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        if (interpreter.VersionArgument is not null)
        {
            startInfo.ArgumentList.Add(interpreter.VersionArgument);
        }

        startInfo.ArgumentList.Add(helperPath);
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add(maxFileSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动本地 PDF 读取进程");
        var standardError = process.StandardError.ReadToEndAsync();
        var standardOutput = process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();
        await standardOutput;
        return new ProcessResult(process.ExitCode, (await standardError).Trim());
    }

    private sealed record PythonInterpreter(string Command, string? VersionArgument);

    private sealed record ProcessResult(int ExitCode, string Error);
}
