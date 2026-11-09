using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TimeClock.Api;

/// <summary>
/// Windows picker shown when the API starts in Development.
/// Closing the window cancels startup. The hosted Render process never calls this.
/// </summary>
[SupportedOSPlatform("windows")]
static class DebugDatabaseDialog
{
    const int ProductionId = 101;
    const int TestId = 102;

    const int WsChild = 0x40000000;
    const int WsVisible = 0x10000000;
    const int WsTabStop = 0x00010000;
    const int WsCaption = 0x00C00000;
    const int WsSysMenu = 0x00080000;
    const int WsClipChildren = 0x02000000;
    const int BsDefPushButton = 0x00000001;
    const int SsNoPrefix = 0x00000080;

    const int WsExTopMost = 0x00000008;
    const int WsExDlgModalFrame = 0x00000001;

    const uint WmSetFont = 0x0030;
    const uint WmCommand = 0x0111;
    const uint WmClose = 0x0010;
    const uint WmDestroy = 0x0002;
    const uint WmKeyDown = 0x0100;
    const int VkEscape = 0x1B;

    const int ColorBtnFace = 15;
    const int IdcArrow = 32512;
    const int IdiApplication = 32512;
    const int DefaultGuiQuality = 5;

    static readonly WndProcDelegate WndProc = WindowProc;
    static string? _choice;
    static string _productionDatabase = "";
    static string _testDatabase = "";
    static IntPtr _font;

    public static string? Choose(string productionDatabase, string testDatabase, string server, bool serverIsLocal)
    {
        _choice = null;
        _productionDatabase = productionDatabase;
        _testDatabase = testDatabase;

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Show(productionDatabase, testDatabase, server, serverIsLocal);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (error is not null)
            throw error;

        return _choice;
    }

    static void Show(string productionDatabase, string testDatabase, string server, bool serverIsLocal)
    {
        SetProcessDPIAware();
        var dpi = (int)GetDpiForSystem();
        if (dpi <= 0)
            dpi = 96;

        int Scale(int px) => px * dpi / 96;

        _font = CreateFont(-(11 * dpi / 72), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, DefaultGuiQuality, 0, "Segoe UI");

        var className = "TimeClockDebugDatabasePicker";
        var instance = GetModuleHandle(null);
        var windowClass = new WndClass
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProc),
            hInstance = instance,
            hIcon = LoadIcon(IntPtr.Zero, (IntPtr)IdiApplication),
            hCursor = LoadCursor(IntPtr.Zero, (IntPtr)IdcArrow),
            hbrBackground = (IntPtr)(ColorBtnFace + 1),
            lpszClassName = className,
        };

        if (RegisterClass(ref windowClass) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410)
                throw new Win32Exception(error);
        }

        const int style = WsCaption | WsSysMenu | WsVisible | WsClipChildren;
        const int exStyle = WsExTopMost | WsExDlgModalFrame;
        var clientWidth = Scale(520);
        var y = Scale(18);
        var labelWidth = clientWidth - Scale(40);
        var labelX = Scale(20);

        var controls = new List<(string Class, string Text, int X, int Y, int W, int H, int Id, int ExtraStyle)>();

        void AddLabel(string text, int height)
        {
            controls.Add(("STATIC", text, labelX, y, labelWidth, height, 0, SsNoPrefix));
            y += height + Scale(8);
        }

        AddLabel("Connect this debug session to:", Scale(22));
        AddLabel($"Server: {server}", Scale(22));
        if (serverIsLocal)
        {
            AddLabel(
                "This host is local Postgres, not Render. Set the DATABASE_URL user secret to the Render external URL. These buttons only change the database name.",
                Scale(68));
        }

        AddLabel("Click a database. Closing this window cancels startup.", Scale(22));

        var buttonHeight = Scale(36);
        controls.Add(("BUTTON", $"Production ({productionDatabase})", labelX, y, labelWidth, buttonHeight, ProductionId, 0));
        y += buttonHeight + Scale(10);
        controls.Add(("BUTTON", $"Test ({testDatabase})", labelX, y, labelWidth, buttonHeight, TestId, BsDefPushButton));
        y += buttonHeight + Scale(18);

        var rect = new Rect { Right = clientWidth, Bottom = y };
        if (!AdjustWindowRectEx(ref rect, style, false, exStyle))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var windowWidth = rect.Right - rect.Left;
        var windowHeight = rect.Bottom - rect.Top;
        var screenW = GetSystemMetrics(0);
        var screenH = GetSystemMetrics(1);
        var windowX = Math.Max(0, (screenW - windowWidth) / 2);
        var windowY = Math.Max(0, (screenH - windowHeight) / 2);

        var hwnd = CreateWindowEx(
            exStyle,
            className,
            "TimeClock",
            style,
            windowX,
            windowY,
            windowWidth,
            windowHeight,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        IntPtr testButton = IntPtr.Zero;
        foreach (var control in controls)
        {
            var childStyle = WsChild | WsVisible | control.ExtraStyle;
            if (control.Class == "BUTTON")
                childStyle |= WsTabStop;

            var child = CreateWindowEx(
                0,
                control.Class,
                control.Text,
                childStyle,
                control.X,
                control.Y,
                control.W,
                control.H,
                hwnd,
                (IntPtr)control.Id,
                instance,
                IntPtr.Zero);
            ApplyFont(child);
            if (control.Id == TestId)
                testButton = child;
        }

        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);
        if (testButton != IntPtr.Zero)
            SetFocus(testButton);

        while (true)
        {
            var status = GetMessage(out var message, IntPtr.Zero, 0, 0);
            if (status == 0)
                break;
            if (status < 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!IsDialogMessage(hwnd, ref message))
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }

        if (_font != IntPtr.Zero)
        {
            DeleteObject(_font);
            _font = IntPtr.Zero;
        }

        UnregisterClass(className, instance);
    }

    static void ApplyFont(IntPtr control)
    {
        if (_font != IntPtr.Zero && control != IntPtr.Zero)
            SendMessage(control, WmSetFont, _font, (IntPtr)1);
    }

    static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WmCommand:
                var id = (int)(wParam.ToInt64() & 0xFFFF);
                if (id == ProductionId)
                    _choice = _productionDatabase;
                else if (id == TestId)
                    _choice = _testDatabase;
                else
                    break;
                DestroyWindow(hWnd);
                return IntPtr.Zero;
            case WmClose:
                _choice = null;
                DestroyWindow(hWnd);
                return IntPtr.Zero;
            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
            case WmKeyDown when wParam == (IntPtr)VkEscape:
                _choice = null;
                DestroyWindow(hWnd);
                return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WndClass
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ushort RegisterClass(ref WndClass lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string? lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    static extern bool IsDialogMessage(IntPtr hDlg, ref Msg lpMsg);

    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    static extern uint GetDpiForSystem();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool AdjustWindowRectEx(ref Rect lpRect, int dwStyle, bool bMenu, int dwExStyle);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFont(
        int cHeight,
        int cWidth,
        int cEscapement,
        int cOrientation,
        int cWeight,
        uint bItalic,
        uint bUnderline,
        uint bStrikeOut,
        uint iCharSet,
        uint iOutPrecision,
        uint iClipPrecision,
        uint iQuality,
        uint iPitchAndFamily,
        string pszFaceName);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr ho);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string? lpModuleName);
}
