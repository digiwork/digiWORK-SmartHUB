using CompanyDirectory.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using Windows.System;

namespace CompanyDirectory_Desktop.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    // ── Win32 constants ──────────────────────────────────────────────────────
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_QUIT   = 0x0012;
    private const int  HOTKEY_ID = 9000;

    private const uint MOD_ALT      = 0x0001;
    private const uint MOD_CONTROL  = 0x0002;
    private const uint MOD_SHIFT    = 0x0004;
    private const uint MOD_WIN      = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    // ── Win32 structures ─────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint   message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint   time;
        public int    ptX;
        public int    ptY;
    }

    // ── Win32 imports ────────────────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpmsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    // ── State ────────────────────────────────────────────────────────────────
    private readonly ILogger<GlobalHotkeyService> _logger;
    private readonly uint _modifiers;
    private readonly uint _vk;

    private volatile uint _threadId;
    private Thread? _thread;
    private bool _disposed;

    private readonly string _modifierStr;
    private readonly string _keyStr;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyService(
        IOptions<HotkeySettings> options,
        ILogger<GlobalHotkeyService> logger)
    {
        _logger      = logger;
        var s        = options.Value;
        _modifierStr = s.Modifiers;
        _keyStr      = s.Key;
        _modifiers   = ParseModifiers(s.Modifiers) | MOD_NOREPEAT;
        _vk          = ParseVirtualKey(s.Key);

        _logger.LogInformation("Hotkey configured: modifiers=0x{Mod:X} vk=0x{Vk:X} ({Mods}+{Key})",
            _modifiers, _vk, s.Modifiers, s.Key);
    }

    public void Start()
    {
        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "GlobalHotkeyThread"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void MessageLoop()
    {
        _threadId = GetCurrentThreadId();

        if (!RegisterHotKey(IntPtr.Zero, HOTKEY_ID, _modifiers, _vk))
        {
            var err = Marshal.GetLastWin32Error();
            _logger.LogWarning("RegisterHotKey failed (Win32 error {Error}). " +
                "Another application may already use the same hotkey.", err);
            var enqueued = App.DispatcherQueue.TryEnqueue(() =>
                App.Services.GetRequiredService<ToastNotificationService>()
                   .ShowHotkeyConflict(_modifierStr, _keyStr));
            if (!enqueued)
                _logger.LogWarning("Failed to enqueue hotkey conflict toast — DispatcherQueue unavailable");
            return;
        }

        _logger.LogInformation("Global hotkey registered (thread {Thread})", _threadId);

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY && (int)msg.wParam == HOTKEY_ID)
            {
                _logger.LogDebug("Global hotkey fired");
                App.DispatcherQueue.TryEnqueue(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        _logger.LogInformation("Global hotkey unregistered");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var tid = _threadId;
        if (tid != 0)
            PostThreadMessage(tid, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────
    private static uint ParseModifiers(string modifiers)
    {
        uint result = 0;
        foreach (var part in modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part.ToUpperInvariant() switch
            {
                "ALT"     => MOD_ALT,
                "CONTROL" => MOD_CONTROL,
                "CTRL"    => MOD_CONTROL,
                "SHIFT"   => MOD_SHIFT,
                "WIN"     => MOD_WIN,
                _         => 0u
            };
        }
        return result;
    }

    private static uint ParseVirtualKey(string key)
    {
        // Windows.System.VirtualKey values match Win32 VK codes
        if (Enum.TryParse<VirtualKey>(key, ignoreCase: true, out var vk))
            return (uint)vk;

        // Fallback: single ASCII character
        if (key.Length == 1)
            return (uint)char.ToUpperInvariant(key[0]);

        return 0;
    }
}
