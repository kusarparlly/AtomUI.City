using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class DogfoodBootstrap
{
    private static readonly object SyncRoot = new();
    private static IApplicationHost? _host;
    private static DogfoodRunOptions? _options;

    public static IApplicationHost Host
    {
        get
        {
            lock (SyncRoot)
            {
                return _host ?? throw new InvalidOperationException("The Dogfood Host is not attached.");
            }
        }
    }

    public static DogfoodRunOptions Options
    {
        get
        {
            lock (SyncRoot)
            {
                return _options ?? throw new InvalidOperationException("The Dogfood run options are not attached.");
            }
        }
    }

    public static void Attach(IApplicationHost host, DogfoodRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(options);

        lock (SyncRoot)
        {
            if (_host is not null)
            {
                throw new InvalidOperationException("Only one Dogfood Host can be attached to the desktop lifetime.");
            }

            _host = host;
            _options = options;
        }
    }

    public static void Detach(IApplicationHost host)
    {
        lock (SyncRoot)
        {
            if (!ReferenceEquals(_host, host))
            {
                return;
            }

            _host = null;
            _options = null;
        }
    }
}
