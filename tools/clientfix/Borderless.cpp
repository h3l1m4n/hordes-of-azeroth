#include "ClientFix.h"
#include "ClientLua.h"

#include <cstdio>
#include <cstring>

namespace
{
    namespace Lua = ClientLua;

    constexpr char PREFERENCE_FOLDER[] = "HordesOfAzeroth";
    constexpr char PREFERENCE_PATH[] = "HordesOfAzeroth\\borderless.txt";
    constexpr char CONFIG_PATH[] = "WTF\\Config.wtf";
    constexpr LONG_PTR FRAME_STYLES = WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
    constexpr LONG_PTR FRAME_EX_STYLES = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_DLGMODALFRAME | WS_EX_STATICEDGE;

    HWND s_window = nullptr;
    WNDPROC s_originalProc = nullptr;
    bool s_unicodeWindow = false;
    bool s_active = false;
    LONG_PTR s_framedStyle = 0;
    LONG_PTR s_framedExStyle = 0;

    bool PreferenceOn()
    {
        FILE* file = nullptr;
        if (fopen_s(&file, PREFERENCE_PATH, "r") != 0 || !file)
            return true;
        int const first = fgetc(file);
        fclose(file);
        return first != '0';
    }

    void SavePreference(bool on)
    {
        CreateDirectoryA(PREFERENCE_FOLDER, nullptr);
        FILE* file = nullptr;
        if (fopen_s(&file, PREFERENCE_PATH, "w") != 0 || !file)
            return;
        fputs(on ? "1\n" : "0\n", file);
        fclose(file);
    }

    bool ConfigWindowed()
    {
        FILE* file = nullptr;
        if (fopen_s(&file, CONFIG_PATH, "r") != 0 || !file)
            return true;
        bool windowed = true;
        char line[256];
        while (fgets(line, sizeof(line), file))
            if (std::strstr(line, "SET gxWindow \""))
                windowed = std::strstr(line, "\"0\"") == nullptr;
        fclose(file);
        return windowed;
    }

    MONITORINFO Monitor(HWND window)
    {
        MONITORINFO info = { sizeof(info) };
        GetMonitorInfoW(MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY), &info);
        return info;
    }

    LONG_PTR WithoutFrame(LONG_PTR style)
    {
        return (style & ~FRAME_STYLES) | WS_POPUP;
    }

    void MoveTo(HWND window, RECT const& area)
    {
        SetWindowPos(window, nullptr, area.left, area.top, area.right - area.left, area.bottom - area.top,
            SWP_FRAMECHANGED | SWP_NOZORDER | SWP_NOOWNERZORDER);
    }

    void FillMonitor(HWND window)
    {
        if (IsZoomed(window))
            ShowWindow(window, SW_RESTORE);
        SetWindowLongPtrW(window, GWL_STYLE, WithoutFrame(GetWindowLongPtrW(window, GWL_STYLE)));
        SetWindowLongPtrW(window, GWL_EXSTYLE, GetWindowLongPtrW(window, GWL_EXSTYLE) & ~FRAME_EX_STYLES);
        MoveTo(window, Monitor(window).rcMonitor);
    }

    void FitWorkArea(HWND window)
    {
        RECT const work = Monitor(window).rcWork;
        RECT current = {};
        GetWindowRect(window, &current);
        if (current.right - current.left <= work.right - work.left && current.bottom - current.top <= work.bottom - work.top)
            return;
        MoveTo(window, work);
    }

    void RestoreFrame(HWND window)
    {
        SetWindowLongPtrW(window, GWL_STYLE, s_framedStyle | (GetWindowLongPtrW(window, GWL_STYLE) & WS_VISIBLE));
        SetWindowLongPtrW(window, GWL_EXSTYLE, s_framedExStyle);
        MoveTo(window, Monitor(window).rcWork);
    }

    void KeepOnMonitor(HWND window, WINDOWPOS* position)
    {
        if (IsIconic(window) || (position->flags & SWP_NOSIZE && position->flags & SWP_NOMOVE))
            return;
        RECT const monitor = Monitor(window).rcMonitor;
        position->x = monitor.left;
        position->y = monitor.top;
        position->cx = monitor.right - monitor.left;
        position->cy = monitor.bottom - monitor.top;
        position->flags &= ~(SWP_NOSIZE | SWP_NOMOVE);
    }

    LRESULT CALLBACK BorderlessWindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (s_active)
        {
            if (message == WM_STYLECHANGING && wParam == WPARAM(GWL_STYLE))
            {
                STYLESTRUCT* styles = reinterpret_cast<STYLESTRUCT*>(lParam);
                styles->styleNew = DWORD(WithoutFrame(styles->styleNew));
            }
            else if (message == WM_NCCALCSIZE && wParam)
                return 0;
            else if (message == WM_WINDOWPOSCHANGING)
                KeepOnMonitor(window, reinterpret_cast<WINDOWPOS*>(lParam));
        }
        return s_unicodeWindow ? CallWindowProcW(s_originalProc, window, message, wParam, lParam)
                               : CallWindowProcA(s_originalProc, window, message, wParam, lParam);
    }

    void Apply(bool borderless, bool windowed)
    {
        bool const wasActive = s_active;
        s_active = borderless && windowed;
        if (s_active)
            FillMonitor(s_window);
        else if (wasActive && windowed)
            RestoreFrame(s_window);
        else if (windowed)
            FitWorkArea(s_window);
        RECT client = {};
        GetClientRect(s_window, &client);
        Log("borderless: %s, %s, client %ldx%ld", borderless ? "on" : "off", windowed ? "windowed" : "fullscreen", client.right, client.bottom);
    }

    int __fastcall Lua_SetBorderless(void* L)
    {
        bool const borderless = Lua::toboolean(L, 1) != 0;
        bool const windowed = Lua::toboolean(L, 2) != 0;
        SavePreference(borderless);
        if (s_originalProc)
            Apply(borderless, windowed);
        return 0;
    }

    int __fastcall Lua_GetBorderless(void* L)
    {
        Lua::pushboolean(L, PreferenceOn() ? 1 : 0);
        return 1;
    }
}

void PrepareBorderless()
{
    if (!PreferenceOn() || !ConfigWindowed())
        return;
    using SetAwarenessContext_t = BOOL(WINAPI*)(DPI_AWARENESS_CONTEXT);
    SetAwarenessContext_t const setAwarenessContext = reinterpret_cast<SetAwarenessContext_t>(
        GetProcAddress(GetModuleHandleW(L"user32.dll"), "SetProcessDpiAwarenessContext"));
    bool const aware = (setAwarenessContext && setAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) || SetProcessDPIAware();
    Log("borderless: sharp pixels %s", aware ? "on" : "OFF");
}

void StartBorderless(HWND window)
{
    if (s_originalProc)
        return;
    s_window = window;
    s_framedStyle = GetWindowLongPtrW(window, GWL_STYLE) & ~WS_VISIBLE;
    s_framedExStyle = GetWindowLongPtrW(window, GWL_EXSTYLE);
    if (!(s_framedStyle & WS_CAPTION))
        s_framedStyle = WS_OVERLAPPEDWINDOW | WS_CLIPSIBLINGS | WS_CLIPCHILDREN;
    s_unicodeWindow = IsWindowUnicode(window) != 0;
    LONG_PTR const proc = reinterpret_cast<LONG_PTR>(&BorderlessWindowProc);
    s_originalProc = reinterpret_cast<WNDPROC>(s_unicodeWindow ? SetWindowLongPtrW(window, GWLP_WNDPROC, proc)
                                                               : SetWindowLongPtrA(window, GWLP_WNDPROC, proc));
    if (!s_originalProc)
    {
        Log("borderless: could not hook the game window");
        return;
    }
    Apply(PreferenceOn(), ConfigWindowed());
}

void RegisterBorderlessFunctions()
{
    namespace Lua = ClientLua;
    Lua::Register("HoAWindow_SetBorderless", &Lua_SetBorderless);
    Lua::Register("HoAWindow_GetBorderless", &Lua_GetBorderless);
}
