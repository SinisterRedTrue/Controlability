using Controlability.Engine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Controlability.App.Services;

/// <summary>Starts the engine with the host and stops it (which gives hidden controllers back) when the host stops.</summary>
public sealed class EngineHostedService(IEngine engine, ILogger<EngineHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            engine.Start();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The engine failed to start.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            engine.Stop();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The engine failed to stop cleanly.");
        }

        return Task.CompletedTask;
    }
}
