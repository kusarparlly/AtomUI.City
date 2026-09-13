using System.Runtime.InteropServices;

namespace AtomUI.City.Fixtures.DesktopDogfood.GuiAutomation;

internal static class NativeDesktop
{
    public const ushort VkTab = 0x09;
    public const ushort VkReturn = 0x0D;
    public const ushort VkEscape = 0x1B;
    public const ushort VkHome = 0x24;
    public const ushort VkEnd = 0x23;
    public const ushort VkDown = 0x28;
    public const ushort VkRight = 0x27;
    public const ushort VkA = 0x41;
    public const ushort VkF4 = 0x73;

    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkF12 = 0x7B;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseWheel = 0x0800;
    private const uint MouseVirtualDesk = 0x4000;
    private const uint MouseAbsolute = 0x8000;
    private const uint KeyExtended = 0x0001;
    private const uint KeyUp = 0x0002;
    private const uint KeyUnicode = 0x0004;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int SwRestore = 9;
    private const uint SwpNoActivate = 0x0010;
    private const uint DesktopSwitchDesktop = 0x0100;
    private const uint SemNoGpFaultErrorBox = 0x0002;

    public static void EnsureInteractiveDesktop()
    {
        if (!Environment.UserInteractive)
        {
            throw new InvalidOperationException("GUI automation requires an interactive Windows user session.");
        }

        var desktop = OpenInputDesktop(0, inherit: false, DesktopSwitchDesktop);
        if (desktop == 0)
        {
            throw new InvalidOperationException("The Windows input desktop is unavailable or locked.");
        }

        CloseDesktop(desktop);
    }

    public static void DisableWindowsCrashDialogs() =>
        SetErrorMode(GetErrorMode() | SemNoGpFaultErrorBox);

    public static NativePoint GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new InvalidOperationException("Could not read the system cursor position.");
        }

        return point;
    }

    public static bool RestoreCursor(NativePoint point) => SetCursorPos(point.X, point.Y);

    public static int GetWindowProcessId(nint windowHandle)
    {
        GetWindowThreadProcessId(windowHandle, out var processId);
        return processId;
    }

    public static IReadOnlyList<nint> EnumerateTopLevelWindows(int processId)
    {
        var handles = new List<nint>();
        EnumWindows((windowHandle, _) =>
        {
            if (IsWindowVisible(windowHandle) && GetWindowProcessId(windowHandle) == processId)
            {
                handles.Add(windowHandle);
            }

            return true;
        }, 0);
        return handles;
    }

    public static void ActivateWindow(nint windowHandle, int processId)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("A native window handle is required.", nameof(windowHandle));
        }
        if (GetWindowProcessId(windowHandle) != processId)
        {
            throw new InvalidOperationException("The target window does not belong to the expected process.");
        }

        var currentThreadId = GetCurrentThreadId();
        var targetThreadId = GetWindowThreadProcessId(windowHandle, out _);
        var foregroundWindow = GetForegroundWindow();
        var foregroundThreadId = foregroundWindow == 0
            ? 0
            : GetWindowThreadProcessId(foregroundWindow, out _);
        var attachedToForeground = false;
        var attachedToTarget = false;
        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, attach: true);
            }
            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedToTarget = AttachThreadInput(currentThreadId, targetThreadId, attach: true);
            }

            ShowWindow(windowHandle, SwRestore);
            BringWindowToTop(windowHandle);
            SetForegroundWindow(windowHandle);
            SetFocus(windowHandle);
        }
        finally
        {
            if (attachedToTarget)
            {
                AttachThreadInput(currentThreadId, targetThreadId, attach: false);
            }
            if (attachedToForeground)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, attach: false);
            }
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var foreground = GetForegroundWindow();
            if (foreground == windowHandle)
            {
                return;
            }

            Thread.Sleep(50);
            BringWindowToTop(windowHandle);
            SetForegroundWindow(windowHandle);
        }

        throw new InvalidOperationException("DesktopDogfood could not acquire foreground input focus.");
    }

    public static void MoveWindow(nint windowHandle, System.Drawing.Rectangle bounds, bool activate = true)
    {
        var flags = activate ? 0u : SwpNoActivate;
        if (!SetWindowPos(
                windowHandle,
                0,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                flags))
        {
            throw new InvalidOperationException("Could not move or resize the DesktopDogfood window.");
        }
    }

    public static void Click(double x, double y)
    {
        ThrowIfEmergencyAbortRequested();
        SendMouseMove(x, y);
        Send([
            MouseInput(MouseLeftDown),
            MouseInput(MouseLeftUp),
        ]);
    }

    public static void Wheel(double x, double y, int detents)
    {
        ThrowIfEmergencyAbortRequested();
        SendMouseMove(x, y);
        Send([MouseInput(MouseWheel, unchecked((uint)(detents * 120)))]);
    }

    public static void Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ThrowIfEmergencyAbortRequested();
        foreach (var character in value)
        {
            Send([
                KeyboardInput(0, character, KeyUnicode),
                KeyboardInput(0, character, KeyUnicode | KeyUp),
            ]);
        }
    }

    public static void Key(ushort virtualKey, bool control = false, bool alt = false)
    {
        ThrowIfEmergencyAbortRequested();
        var inputs = new List<Input>();
        if (control)
        {
            inputs.Add(KeyboardInput(VkControl));
        }
        if (alt)
        {
            inputs.Add(KeyboardInput(0x12));
        }

        var extended = virtualKey is VkHome or VkEnd or VkDown or VkRight ? KeyExtended : 0u;
        inputs.Add(KeyboardInput(virtualKey, flags: extended));
        inputs.Add(KeyboardInput(virtualKey, flags: extended | KeyUp));
        if (alt)
        {
            inputs.Add(KeyboardInput(0x12, flags: KeyUp));
        }
        if (control)
        {
            inputs.Add(KeyboardInput(VkControl, flags: KeyUp));
        }

        Send(inputs.ToArray());
    }

    public static void ThrowIfEmergencyAbortRequested()
    {
        if (IsKeyDown(VkControl) && IsKeyDown(VkShift) && IsKeyDown(VkF12))
        {
            throw new GuiAutomationAbortedException(
                "GUI automation was aborted by Ctrl+Shift+F12.");
        }
    }

    public static string CaptureScreen(
        System.Drawing.Rectangle bounds,
        string destinationPath)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "Screenshot bounds must be non-empty.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var bitmap = new System.Drawing.Bitmap(
            bounds.Width,
            bounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                bounds.Location,
                System.Drawing.Point.Empty,
                bounds.Size,
                System.Drawing.CopyPixelOperation.SourceCopy);
        }

        EnsureImageHasContent(bitmap, destinationPath);
        bitmap.Save(destinationPath, System.Drawing.Imaging.ImageFormat.Png);
        return destinationPath;
    }

    private static void EnsureImageHasContent(System.Drawing.Bitmap bitmap, string path)
    {
        var colors = new HashSet<int>();
        var xStep = Math.Max(1, bitmap.Width / 64);
        var yStep = Math.Max(1, bitmap.Height / 64);
        for (var y = 0; y < bitmap.Height; y += yStep)
        {
            for (var x = 0; x < bitmap.Width; x += xStep)
            {
                colors.Add(bitmap.GetPixel(x, y).ToArgb());
                if (colors.Count >= 8)
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException($"GUI screenshot '{path}' did not contain distinguishable pixels.");
    }

    private static void SendMouseMove(double x, double y)
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 1 || height <= 1)
        {
            throw new InvalidOperationException("The Windows virtual desktop has invalid dimensions.");
        }

        var normalizedX = (int)Math.Round((x - left) * 65535d / (width - 1));
        var normalizedY = (int)Math.Round((y - top) * 65535d / (height - 1));
        Send([new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInputData
                {
                    Dx = normalizedX,
                    Dy = normalizedY,
                    Flags = MouseMove | MouseAbsolute | MouseVirtualDesk,
                },
            },
        }]);
    }

    private static Input MouseInput(uint flags, uint mouseData = 0) => new()
    {
        Type = InputMouse,
        Data = new InputUnion
        {
            Mouse = new MouseInputData
            {
                MouseData = mouseData,
                Flags = flags,
            },
        },
    };

    private static Input KeyboardInput(ushort virtualKey, char scanCode = '\0', uint flags = 0) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInputData
            {
                VirtualKey = virtualKey,
                ScanCode = scanCode,
                Flags = flags,
            },
        },
    };

    private static void Send(Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"Windows SendInput accepted {sent} of {inputs.Length} input records; error={Marshal.GetLastWin32Error()}.");
        }
    }

    private static bool IsKeyDown(ushort key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInputData Mouse;

        [FieldOffset(0)]
        public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint currentThreadId, uint targetThreadId, bool attach);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out int processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

    [DllImport("user32.dll")]
    private static extern bool CloseDesktop(nint desktop);

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);
}

internal sealed class GuiAutomationAbortedException : OperationCanceledException
{
    public GuiAutomationAbortedException(string message)
        : base(message)
    {
    }
}
