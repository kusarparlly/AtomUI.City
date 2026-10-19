using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm.Tests;

internal sealed class ThrowingHostDiagnostics : IHostDiagnostics
{
    public IReadOnlyList<HostDiagnosticRecord> Records => [];

    public void Write(HostDiagnosticRecord record)
    {
        throw new InvalidOperationException("diagnostics-failure");
    }

    public void Complete()
    {
    }
}
