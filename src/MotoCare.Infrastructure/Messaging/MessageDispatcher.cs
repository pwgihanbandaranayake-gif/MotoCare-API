using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MotoCare.Application.Services;

namespace MotoCare.Infrastructure.Messaging;

/// <summary>Added: background sender. Every 30 seconds it sends pending e-mail / WhatsApp messages from the outbox.</summary>
public sealed class MessageDispatcher(IServiceScopeFactory scopes, ILogger<MessageDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MessageService>().ProcessPendingAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Message dispatcher run failed");   // e.g. database not reachable - try again next time
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
