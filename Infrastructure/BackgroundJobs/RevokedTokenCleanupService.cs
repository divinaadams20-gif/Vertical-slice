namespace Infrastructure.BackgroundJobs;

using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class RevokedTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RevokedTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await dbContext.RevokedTokens
                .Where(token => token.ExpiresAtUtc < DateTime.UtcNow)
                .ExecuteDeleteAsync(stoppingToken);

            if (deleted > 0)
            {
                logger.LogInformation("Cleaned up {Count} expired revoked tokens.", deleted);
            }
        }
    }
}