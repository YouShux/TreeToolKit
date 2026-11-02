using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace TreeToolKit;

public sealed unsafe class SafeDropDownNode<T> : DropDownNode<T>
{
    private static readonly PropertyInfo PopupListProperty = typeof(DropDownNode<T>).GetProperty(
        "PopupButtonListNode", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(DropDownNode<T>).FullName, "PopupButtonListNode");

    private readonly VerticalListNode popupList;

    public SafeDropDownNode()
    {
        popupList = (VerticalListNode)PopupListProperty.GetValue(this)!;
        base.OnCollapsed = HandleCollapsed;
        base.OnUncollapsed = HandleUncollapsed;
    }

    public new Action? OnCollapsed { get; set; }
    public new Action? OnUncollapsed { get; set; }

    public new List<T> Options
    {
        get => base.Options;
        set
        {
            PrepareRebuild(Math.Min(MaxListOptions, value.Count));
            base.Options = value;
            SynchronizeAddon();
        }
    }

    public new int MaxListOptions
    {
        get => base.MaxListOptions;
        set
        {
            PrepareRebuild(Math.Min(value, Options.Count));
            base.MaxListOptions = value;
            SynchronizeAddon();
        }
    }

    public void ClosePopup()
    {
        if (IsCollapsed)
            return;

        var enabled = IsEnabled;
        IsEnabled = true;
        try
        {
            Collapse(false);
        }
        finally
        {
            IsEnabled = enabled;
        }
    }

    private void PrepareRebuild(int buttonCount)
    {
        AtkResNode* nativeList = popupList;
        var parent = nativeList->ParentNode;
        if (parent is null || Math.Abs(parent->Height - (buttonCount * 22f + 18f)) <= 0.1f)
            return;

        foreach (var node in popupList.Nodes)
            NativeNodeDetach.DetachAndDestroy(node);
    }

    private void HandleCollapsed()
    {
        SynchronizeAddon();
        OnCollapsed?.Invoke();
    }

    private void HandleUncollapsed()
    {
        SynchronizeAddon();
        OnUncollapsed?.Invoke();
    }

    private void SynchronizeAddon()
    {
        var manager = RaptureAtkUnitManager.Instance();
        var addon = manager is null ? null : manager->GetAddonByNode(this);
        if (addon is null || addon->RootNode is null)
            return;

        ushort childCount = 0;
        for (var child = addon->RootNode->ChildNode; child is not null; child = child->PrevSiblingNode)
            childCount++;
        addon->RootNode->ChildCount = childCount;
        NativeNodeDetach.UpdateNodeLists(addon);
    }
}
