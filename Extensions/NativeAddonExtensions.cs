using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;

namespace TreeToolKit;

public static unsafe class NativeAddonExtensions
{
    /// <summary>打开窗口；已打开时将其置于前台。必须在游戏主线程调用。</summary>
    public static void OpenOrFocus(this NativeAddon window)
    {
        ThreadSafety.AssertMainThread();
        if (!window.IsOpen)
        {
            window.Open();
            return;
        }

        AtkUnitBase* addon = window;
        addon->Focus();
    }
}
