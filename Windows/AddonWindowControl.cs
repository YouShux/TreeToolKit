using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools.Interop.Game.Helpers;

namespace TreeToolKit;

public static unsafe class AddonWindowControl
{
    public static void Close(string addonName)
    {
        if (!AddonHelper.TryGetByName(addonName, out var addon) || !addon->IsVisible)
        {
            return;
        }

        try
        {
            addon->Close(true);
        }
        catch (Exception)
        {
        }
    }
}
