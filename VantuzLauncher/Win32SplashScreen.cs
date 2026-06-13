using System;
using System.Runtime.InteropServices;

namespace VantuzLauncher;

/// <summary>
/// F_doc: {Splash screen not shown within 100ms of process start, or window handle invalid}
/// E_doc: Timer-based integration test measures Show() to FindWindow() latency; fails if >100ms
/// </summary>
public static class Win32SplashScreen
{
    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_CLIPSIBLINGS = 0x04000000;
    private const uint WS_CLIPCHILDREN = 0x02000000;
    private static nint _hWnd = nint.Zero;
    private static nint _hInstance = nint.Zero;

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint DefWindowProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private delegate nint WndProcDelegate(nint hWnd, uint uMsg, nint wParam, nint lParam);

    private const int SM_CXSCREEN = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    private const uint WM_DESTROY = 0x0002;
    private const uint WM_PAINT = 0x000F;
    private const int SW_SHOW = 5;

    private static nint SplashWndProc(nint hWnd, uint uMsg, nint wParam, nint lParam)
    {
        if (uMsg == WM_PAINT)
        {
            // Minimal: OS draws background; no custom painting needed for speed
            return DefWindowProc(hWnd, uMsg, wParam, lParam);
        }
        return DefWindowProc(hWnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Shows a lightweight native splash window centered on screen.
    /// Total latency target: <100ms from method entry to visible pixel.
    /// F_doc: {Show does not create visible window within 100ms or returns without showing}
    /// E_doc: Timer-based test measures latency; fails if >100ms
    /// </summary>
    public static void Show(string title = "Vantuz Launcher", int width = 400, int height = 200)
    {
        if (_hWnd != nint.Zero) return;

        _hInstance = GetModuleHandle(null);
        var className = "VantuzSplash";
        var wndClass = new WNDCLASS
        {
            style = 0,
            lpfnWndProc = SplashWndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = _hInstance,
            hIcon = nint.Zero,
            hCursor = nint.Zero,
            hbrBackground = new nint(1 + 0), // COLOR_WINDOW
            lpszMenuName = null,
            lpszClassName = className
        };

        RegisterClass(ref wndClass);

        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int x = (screenW - width) / 2;
        int y = 200;

        _hWnd = CreateWindowEx(
            0, className, title,
            WS_POPUP | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
            x, y, width, height,
            nint.Zero, nint.Zero, _hInstance, nint.Zero);

        if (_hWnd != nint.Zero)
        {
            ShowWindow(_hWnd, SW_SHOW);
            UpdateWindow(_hWnd);
        }
    }

    /// <summary>
    /// Closes the splash window and releases the native handle.
    /// F_doc: {Close fails to destroy window or leaks handle}
    /// E_doc: Window handle test confirms _hWnd reset to Zero after call
    /// </summary>
    public static void Close()
    {
        if (_hWnd != nint.Zero)
        {
            DestroyWindow(_hWnd);
            _hWnd = nint.Zero;
        }
    }
}
