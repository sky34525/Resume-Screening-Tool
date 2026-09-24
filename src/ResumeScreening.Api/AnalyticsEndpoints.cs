using System.Net;
using ResumeScreening.Api.Services;

namespace ResumeScreening.Api;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalytics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/analytics",
            (HttpContext context, IAiUsageMetricsStore metricsStore) =>
            {
                var remoteIpAddress = context.Connection.RemoteIpAddress;
                if (remoteIpAddress is null || !IPAddress.IsLoopback(remoteIpAddress))
                {
                    return Results.Forbid();
                }

                return Results.Ok(metricsStore.GetReport());
            });

        return endpoints;
    }
}
