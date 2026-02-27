using Hive_Movie.Data;
using Hive_Movie.Infrastructure.Messaging;
using Hive_Movie.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Hive_Movie.Services.Workers;

public class NotificationOutboxWorker(
    IServiceProvider serviceProvider,
    ILogger<NotificationOutboxWorker> logger)
    : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxRetryCount = 5;
    private readonly static TimeSpan DelayBetweenRuns = TimeSpan.FromSeconds(10);
    private readonly static TimeSpan StuckProcessingTimeout = TimeSpan.FromMinutes(5);

    private readonly static JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification Outbox Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<INotificationProducer>();

                await ResetStuckMessagesAsync(db, stoppingToken);
                var messages = await ClaimMessagesAsync(db, stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        var emailEvent = JsonSerializer.Deserialize<EmailNotificationEvent>(
                            message.Payload,
                            JsonOptions);

                        if (emailEvent is null)
                            throw new InvalidOperationException("Failed to deserialize EmailNotificationEvent.");

                        await producer.SendEmailNotificationAsync(emailEvent, message.Id);

                        message.ProcessedAtUtc = DateTime.UtcNow;
                        message.ErrorMessage = null;

                        logger.LogInformation("Outbox message {MessageId} processed successfully.", message.Id);
                    }
                    catch (Exception ex)
                    {
                        message.RetryCount++;
                        message.ErrorMessage = ex.Message;
                        message.ProcessingAtUtc = null;

                        if (message.RetryCount >= MaxRetryCount)
                        {
                            message.ProcessedAtUtc = DateTime.UtcNow;
                            logger.LogWarning(ex, "Outbox message {MessageId} moved to poison state after {RetryCount} retries.", message.Id,
                                message.RetryCount);
                        }
                        else
                        {
                            logger.LogError(ex, "Error processing Outbox message {MessageId}. Retry {RetryCount}.", message.Id, message.RetryCount);
                        }
                    }
                }

                if (messages.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Fatal error in Outbox Worker loop.");
            }

            try
            {
                await Task.Delay(DelayBetweenRuns, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // graceful shutdown
            }
        }

        logger.LogInformation("Notification Outbox Worker stopped.");
    }

    private async static Task ResetStuckMessagesAsync(ApplicationDbContext db, CancellationToken token)
    {
        var threshold = DateTime.UtcNow - StuckProcessingTimeout;

        var stuckMessages = await db.OutboxMessages
            .Where(m =>
                m.ProcessingAtUtc != null && m.ProcessedAtUtc == null && m.ProcessingAtUtc < threshold)
            .ToListAsync(token);

        foreach (var message in stuckMessages)
        {
            message.ProcessingAtUtc = null;
        }

        if (stuckMessages.Count > 0)
            await db.SaveChangesAsync(token);
    }

    private async static Task<List<OutboxMessage>> ClaimMessagesAsync(ApplicationDbContext db, CancellationToken token)
    {
        return await db.OutboxMessages
            .FromSqlRaw($@"
                WITH CTE AS (
                    SELECT TOP ({BatchSize}) * FROM OutboxMessages WITH (UPDLOCK, READPAST)
                    WHERE ProcessedAtUtc IS NULL
                      AND ProcessingAtUtc IS NULL
                      AND RetryCount < {MaxRetryCount}
                    ORDER BY CreatedAtUtc
                )
                UPDATE CTE
                SET ProcessingAtUtc = GETUTCDATE()
                OUTPUT INSERTED.*;")
            .ToListAsync(token);
    }
}