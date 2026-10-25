using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace TreeToolKit;

public static unsafe class GameWindowClip
{
    private static readonly List<(Vector2 Min, Vector2 Max)> Regions = [];
    private static int LastFrame = -1;

    public static IReadOnlyList<(Vector2 Min, Vector2 Max)> GetVisibleRegions()
    {
        var frame = ImGui.GetFrameCount();
        if (LastFrame == frame)
        {
            return Regions;
        }
        LastFrame = frame;
        Regions.Clear();
        var viewport = ImGui.GetMainViewport();
        Regions.Add((viewport.Pos, viewport.Pos + viewport.Size));
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
        {
            return Regions;
        }

        var units = &manager->AllLoadedUnitsList;
        for (var index = 0; index < units->Count; index++)
        {
            var addon = units->Entries[index].Value;
            if (addon == null || (addon->UiFlags & UiFlags.Chat) != 0 ||
                !addon->IsVisible || addon->RootNode == null ||
                !addon->RootNode->IsVisible() || (addon->VisibilityFlags & 5) != 0 ||
                addon->WindowNode == null || !addon->WindowNode->IsVisible())
            {
                continue;
            }

            Bounds bounds;
            addon->GetWindowBounds(&bounds);
            var minimum = viewport.Pos + new Vector2(bounds.Pos1.X, bounds.Pos1.Y);
            var maximum = viewport.Pos + new Vector2(bounds.Pos2.X, bounds.Pos2.Y);
            for (var regionIndex = Regions.Count - 1; regionIndex >= 0; regionIndex--)
            {
                var region = Regions[regionIndex];
                var min = Vector2.Max(region.Min, minimum);
                var max = Vector2.Min(region.Max, maximum);
                if (min.X >= max.X || min.Y >= max.Y)
                {
                    continue;
                }
                Regions.RemoveAt(regionIndex);
                Add(region.Min, new Vector2(region.Max.X, min.Y));
                Add(new Vector2(region.Min.X, max.Y), region.Max);
                Add(new Vector2(region.Min.X, min.Y), new Vector2(min.X, max.Y));
                Add(new Vector2(max.X, min.Y), new Vector2(region.Max.X, max.Y));
            }
        }
        return Regions;
    }

    public static bool IsCoveredByWindow(AtkUnitBase* source, Vector2 minimum, Vector2 maximum)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
            return false;

        var units = &manager->AllLoadedUnitsList;
        for (var index = 0; index < units->Count; index++)
        {
            var addon = units->Entries[index].Value;
            if (addon == null || addon == source || (addon->UiFlags & UiFlags.Chat) != 0 ||
                !addon->IsVisible || addon->RootNode == null || !addon->RootNode->IsVisible() ||
                (addon->VisibilityFlags & 5) != 0 || addon->WindowNode == null ||
                !addon->WindowNode->IsVisible() ||
                addon->DepthLayer < source->DepthLayer ||
                addon->DepthLayer == source->DepthLayer && addon->DrawOrderIndex <= source->DrawOrderIndex)
                continue;

            Bounds bounds;
            addon->GetWindowBounds(&bounds);
            if (minimum.X < bounds.Pos2.X && maximum.X > bounds.Pos1.X &&
                minimum.Y < bounds.Pos2.Y && maximum.Y > bounds.Pos1.Y)
                return true;
        }
        return false;
    }

    private static void Add(Vector2 min, Vector2 max)
    {
        if (min.X < max.X && min.Y < max.Y)
        {
            Regions.Add((min, max));
        }
    }
}
