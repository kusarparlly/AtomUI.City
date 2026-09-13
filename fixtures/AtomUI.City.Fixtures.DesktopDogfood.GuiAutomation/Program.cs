namespace AtomUI.City.Fixtures.DesktopDogfood.GuiAutomation;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("DesktopDogfood GUI automation requires Windows.");
            return 3;
        }

        try
        {
            var options = GuiAutomationOptions.Parse(args);
            using var mutex = new Mutex(
                initiallyOwned: false,
                "AtomUI.City.DesktopDogfood.GuiAutomation");
            if (!mutex.WaitOne(0))
            {
                Console.Error.WriteLine("Another DesktopDogfood GUI automation run is active.");
                return 4;
            }

            try
            {
                return new WindowsGuiAutomationDriver(options).Run();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"DESKTOP_DOGFOOD_GUI_DRIVER_FAILED {exception}");
            return 1;
        }
    }
}
