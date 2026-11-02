using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace TreeToolKit;

public sealed unsafe class SafeListNode<T, TU> : ListNode<T, TU>
    where TU : ListItemNode<T>, IListItemNode, new()
{
    public new float ItemSpacing
    {
        get => base.ItemSpacing;
        set
        {
            var addon = PrepareRebuild();
            base.ItemSpacing = value;
            NativeNodeDetach.UpdateNodeLists(addon);
        }
    }

    public new void FullRebuild()
    {
        var addon = PrepareRebuild();
        base.FullRebuild();
        NativeNodeDetach.UpdateNodeLists(addon);
    }

    protected override void OnSizeChanged()
    {
        var rebuild = (int)(Height / (TU.ItemHeight + ItemSpacing)) != OptionNodes.Count;
        var addon = rebuild ? PrepareRebuild() : null;
        base.OnSizeChanged();
        if (rebuild)
            NativeNodeDetach.UpdateNodeLists(addon);
    }

    private AtkUnitBase* PrepareRebuild()
    {
        var manager = RaptureAtkUnitManager.Instance();
        var addon = manager is null ? null : manager->GetAddonByNode(this);
        NativeNodeDetach.DetachAndDestroy(UpwardsNavNode);
        NativeNodeDetach.DetachAndDestroy(DownwardsNavNode);
        foreach (var node in OptionNodes)
            NativeNodeDetach.DetachAndDestroy(node);
        return addon;
    }
}
