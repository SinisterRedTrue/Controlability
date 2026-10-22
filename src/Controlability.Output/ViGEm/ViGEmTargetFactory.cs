using Nefarius.ViGEm.Client;

namespace Controlability.Output.ViGEm;

/// <summary>Owns the single <see cref="ViGEmClient"/> of the process, created on first use.</summary>
internal sealed class ViGEmTargetFactory : IXboxTargetFactory
{
    private readonly object _gate = new();
    private ViGEmClient? _client;
    private bool _probed;

    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                if (!_probed)
                {
                    _probed = true;
                    try
                    {
                        _client = new ViGEmClient();
                    }
                    catch (Exception)
                    {
                        // VigemBusNotFoundException, or any other failure to reach the bus: treat as missing.
                        _client = null;
                    }
                }

                return _client is not null;
            }
        }
    }

    public IXboxTarget Create()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("ViGEmBus is not available.");
        }

        lock (_gate)
        {
            return new ViGEmXboxTarget(_client!);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _client?.Dispose();
            _client = null;
        }
    }
}
