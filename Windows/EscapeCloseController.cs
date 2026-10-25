using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace TreeToolKit;

public sealed class EscapeCloseController
{
    private readonly IKeyState keyState;
    private bool escapePressedLastFrame;
    private bool suppressEscapeUntilRelease;

    public EscapeCloseController(IKeyState keyState) => this.keyState = keyState ?? throw new ArgumentNullException(nameof(keyState));

    public bool IsSuppressingEscape => suppressEscapeUntilRelease;

    public void Update(
        IEscapeClosableWindow first,
        IEscapeClosableWindow? second = null,
        IEscapeClosableWindow? third = null)
    {
        var focusedWindow = ResolveFocusedWindow(first, second, third);
        if (TryConsumeEscape(focusedWindow is not null) && focusedWindow is not null)
        {
            focusedWindow.Close();
        }
    }

    public bool TryConsumeEscape(bool canClose)
    {
        var escapePressed = (GetAsyncKeyState(ESCAPE_VIRTUAL_KEY) & 0x8000) != 0;
        if (suppressEscapeUntilRelease)
        {
            // 松键帧仍需清除游戏输入，避免关闭插件窗口后触发系统菜单。
            keyState[VirtualKey.ESCAPE] = false;
            escapePressedLastFrame = escapePressed;
            suppressEscapeUntilRelease = escapePressed;
            return false;
        }
        if (!escapePressed)
        {
            escapePressedLastFrame = false;
            return false;
        }

        if (escapePressedLastFrame)
        {
            return false;
        }

        escapePressedLastFrame = true;
        if (!canClose)
        {
            return false;
        }

        suppressEscapeUntilRelease = true;
        keyState[VirtualKey.ESCAPE] = false;
        return true;
    }

    private static IEscapeClosableWindow? ResolveFocusedWindow(
        IEscapeClosableWindow first,
        IEscapeClosableWindow? second,
        IEscapeClosableWindow? third)
    {
        if (first.IsOpen && first.IsFocused)
        {
            return first;
        }

        if (second is { IsOpen: true, IsFocused: true })
        {
            return second;
        }

        return third is { IsOpen: true, IsFocused: true } ? third : null;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    #region 常量

    private const int ESCAPE_VIRTUAL_KEY = 0x1B;

    #endregion
}
