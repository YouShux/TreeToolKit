using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using OmenTools.Extensions;

namespace TreeToolKit;

public static unsafe class NativeAddonAttachment
{
    // 在首次显示和后续跟随时调用；间距与垂直偏移使用源窗口的设计尺寸。
    public static void SyncBeside
    (
        NativeAddon  window,
        AtkUnitBase* addon,
        AtkUnitBase* sourceAddon,
        float        gap,
        float        verticalOffset,
        bool         preferRight = false
    )
    {
        addon->EnableTitleBarContextMenu = false;
        addon->DisableUserScaling        = false;
        if (sourceAddon->WindowNode == null || addon->WindowNode == null)
            return;

        var sourceNode = (AtkResNode*)sourceAddon->WindowNode;
        var targetNode = (AtkResNode*)addon->WindowNode;
        var scale = sourceNode->GetScale();
        var currentScale = targetNode->GetScale();
        if (Vector2.DistanceSquared(currentScale, scale) > 0.000001f)
        {
            addon->SetScale(scale.X / AtkUnitBase.GetGlobalUIScale(), true);
            // 自建窗口的根节点也必须同步，不能仅更新 Addon 的缩放状态。
            currentScale = targetNode->GetScale();
            if (currentScale.X > 0f && currentScale.Y > 0f)
            {
                window.RootNode.ScaleX *= scale.X / currentScale.X;
                window.RootNode.ScaleY *= scale.Y / currentScale.Y;
            }
        }

        var source = sourceNode->GetNodeState();
        var target = targetNode->GetNodeState();
        var x = preferRight ? source.X + source.Width + gap * scale.X : source.X - target.Width - gap * scale.X;
        var desired = new Vector2(x, source.Y + verticalOffset * scale.Y);
        var viewport = ImGui.GetMainViewport();
        var screenStart = viewport.WorkPos - viewport.Pos;
        if (!preferRight && desired.X < screenStart.X)
            desired.X = source.X + source.Width + gap * scale.X;
        desired = Vector2.Clamp(desired, screenStart,
            Vector2.Max(screenStart, screenStart + viewport.WorkSize - new Vector2(target.Width, target.Height)));
        var position = new Vector2(addon->X, addon->Y) + desired - target.TopLeft;
        if (MathF.Abs(addon->X - position.X) > 0.5f || MathF.Abs(addon->Y - position.Y) > 0.5f)
            window.SetWindowPosition(position);
    }
}
