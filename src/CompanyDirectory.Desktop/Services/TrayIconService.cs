using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace CompanyDirectory_Desktop.Services;

public sealed class TrayIconService : IDisposable
{
    // ── Win32 constants ──────────────────────────────────────────────────────
    private const uint WM_DESTROY        = 0x0002;
    private const uint WM_LBUTTONDBLCLK  = 0x0203;
    private const uint WM_RBUTTONUP      = 0x0205;
    private const uint WM_APP_TRAY       = 0x8001;

    private const uint NIM_ADD    = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON    = 0x02;
    private const uint NIF_TIP     = 0x04;
    private const uint NIF_INFO    = 0x10;

    private const uint NIIF_INFO    = 0x01;
    private const uint NIIF_WARNING = 0x02;

    private const uint MF_STRING    = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_LEFTALIGN   = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD   = 0x0100;
    private const uint IMAGE_ICON   = 1;
    private const uint LR_LOADFROMFILE = 0x10;

    private const int CMD_OPEN     = 100;
    private const int CMD_EXIT     = 101;
    private const int CMD_SETTINGS = 102;
    private const int CMD_ADMIN    = 103;
    private const int CMD_INBOX    = 104;
    private const int CMD_SENT     = 105;
    private const int CMD_SMS      = 106;
    private const int CMD_CHAT     = 107;

    // ── Win32 structures ─────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public int  cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public int    cbSize;
        public uint   style;
        public IntPtr lpfnWndProc;
        public int    cbClsExtra;
        public int    cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string  lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    // ── Win32 imports ────────────────────────────────────────────────────────
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpdata);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string? lpWindowName,
        uint dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImageW(IntPtr hInst, string name, uint type,
        int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(IntPtr lpModuleName);

    // ── State ────────────────────────────────────────────────────────────────
    private readonly ILogger<TrayIconService> _logger;
    private IntPtr _hWnd;
    private IntPtr _hIcon;
    private bool   _added;
    private bool   _disposed;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate; // keep alive — must not be GC'd

    public event EventHandler? OpenRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? AdminRequested;
    public event EventHandler? InboxRequested;
    public event EventHandler? SentRequested;
    public event EventHandler? SmsRequested;
    public event EventHandler? ChatRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(ILogger<TrayIconService> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            CreateMessageWindow();
            LoadIcon();
            AddTrayIcon();
            _logger.LogInformation("Tray icon initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tray icon");
        }
    }

    private void CreateMessageWindow()
    {
        _wndProcDelegate = WndProc;
        var hInstance = GetModuleHandleW(IntPtr.Zero);
        const string className = "CompanyDirectory_TrayMsg";

        var wcx = new WNDCLASSEXW
        {
            cbSize      = Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance   = hInstance,
            lpszClassName = className,
        };

        RegisterClassExW(ref wcx); // ignore return value (may already be registered)

        // HWND_MESSAGE = (IntPtr)(-3) — message-only window, invisible, no taskbar entry
        _hWnd = CreateWindowExW(0, className, null, 0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowExW failed: {Marshal.GetLastWin32Error()}");
    }

    private void LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            _hIcon = LoadImageW(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        }

        if (_hIcon == IntPtr.Zero)
        {
            // Fallback — load from current .exe
            var exePath = Environment.ProcessPath;
            if (exePath != null)
                _hIcon = LoadImageW(IntPtr.Zero, exePath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        }
    }

    private void AddTrayIcon()
    {
        var nid = BuildNid();
        nid.uFlags   = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.hIcon    = _hIcon;
        nid.szTip    = "CompanyDirectory";

        _added = Shell_NotifyIconW(NIM_ADD, ref nid);
        if (!_added)
            _logger.LogWarning("Shell_NotifyIconW NIM_ADD failed");
    }

    private void RemoveTrayIcon()
    {
        if (!_added) return;
        var nid = BuildNid();
        Shell_NotifyIconW(NIM_DELETE, ref nid);
        _added = false;
    }

    private NOTIFYICONDATAW BuildNid() => new()
    {
        cbSize          = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd            = _hWnd,
        uID             = 1,
        uCallbackMessage = WM_APP_TRAY,
        szTip           = string.Empty,
        szInfo          = string.Empty,
        szInfoTitle     = string.Empty,
    };

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP_TRAY)
        {
            var notifyMsg = (uint)(lParam.ToInt32() & 0xFFFF);

            if (notifyMsg == WM_LBUTTONDBLCLK)
            {
                App.DispatcherQueue.TryEnqueue(() => OpenRequested?.Invoke(this, EventArgs.Empty));
                return IntPtr.Zero;
            }

            if (notifyMsg == WM_RBUTTONUP)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_OPEN),     "Otwórz wyszukiwarkę");
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_INBOX),    "Skrzynka odebrana");
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_CHAT),     "Komunikator");
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_SENT),     "Historia wiadomości");
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_SETTINGS), "Ustawienia");
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_ADMIN),    "Wyślij wiadomość");
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_SMS),      "Wyślij SMS");
            AppendMenuW(hMenu, MF_SEPARATOR, IntPtr.Zero,              null);
            AppendMenuW(hMenu, MF_STRING,    new IntPtr(CMD_EXIT),     "Zamknij aplikację");

            SetForegroundWindow(_hWnd);
            GetCursorPos(out var pt);

            var cmd = (int)TrackPopupMenuEx(hMenu,
                TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                pt.X, pt.Y, _hWnd, IntPtr.Zero);

            if (cmd == CMD_OPEN)
                App.DispatcherQueue.TryEnqueue(() => OpenRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_INBOX)
                App.DispatcherQueue.TryEnqueue(() => InboxRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_SENT)
                App.DispatcherQueue.TryEnqueue(() => SentRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_SETTINGS)
                App.DispatcherQueue.TryEnqueue(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_ADMIN)
                App.DispatcherQueue.TryEnqueue(() => AdminRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_SMS)
                App.DispatcherQueue.TryEnqueue(() => SmsRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_CHAT)
                App.DispatcherQueue.TryEnqueue(() => ChatRequested?.Invoke(this, EventArgs.Empty));
            else if (cmd == CMD_EXIT)
                App.DispatcherQueue.TryEnqueue(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    public void SetUnreadCount(int count)
    {
        if (!_added) return;
        var nid    = BuildNid();
        nid.uFlags = NIF_TIP;
        nid.szTip  = count > 0
            ? $"CompanyDirectory — {count} nieprzeczytanych"
            : "CompanyDirectory";
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    public void ShowBalloonTip(string title, string text, bool isWarning = false)
    {
        if (!_added) return;
        var nid         = BuildNid();
        nid.uFlags      = NIF_INFO;
        nid.szInfoTitle = title.Length > 63  ? title[..63]  : title;
        nid.szInfo      = text.Length  > 255 ? text[..255]  : text;
        nid.dwInfoFlags = isWarning ? NIIF_WARNING : NIIF_INFO;
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        RemoveTrayIcon();

        if (_hIcon != IntPtr.Zero) { DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
        if (_hWnd  != IntPtr.Zero) { DestroyWindow(_hWnd); _hWnd  = IntPtr.Zero; }

        _logger.LogInformation("Tray icon disposed");
    }
}
