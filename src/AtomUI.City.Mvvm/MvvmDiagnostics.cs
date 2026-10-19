using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

internal static class MvvmDiagnostics
{
    public static void Write(
        IHostDiagnostics? diagnostics,
        HostDiagnosticRecord record)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(record);
        }
        catch
        {
            // Diagnostics are observational and must never alter MVVM behavior.
        }
    }
}
