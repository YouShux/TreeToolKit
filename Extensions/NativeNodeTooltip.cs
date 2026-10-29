using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using Lumina.Text.ReadOnly;
using OmenTools.Extensions;

namespace TreeToolKit;

public static class NativeNodeTooltip
{
    private static readonly ConditionalWeakTable<NodeBase, TextTooltipState> TextTooltips = new();

    public static void SetTextTooltip
    (
        this NodeBase    node,
        ReadOnlySeString tooltip
    ) =>
        TextTooltips.GetValue(node, static value => new(value)).Text = tooltip;

    public static unsafe void ShowAnchoredTextTooltip
    (
        this NodeBase    node,
        ReadOnlySeString tooltip
    )
    {
        AtkResNode* nativeNode = node;
        if (nativeNode is null || tooltip.IsEmpty)
            return;

        var addon = nativeNode->GetOwnerAddon();
        if (addon is null)
            return;

        AtkStage.Instance()->TooltipManager.ShowTooltip(addon->Id, nativeNode, tooltip);
    }

    private sealed class TextTooltipState
    {
        public TextTooltipState(NodeBase node)
        {
            node.AddEvent(AtkEventType.MouseOver, () => node.ShowAnchoredTextTooltip(Text));
            node.AddEvent(AtkEventType.MouseOut, node.HideTooltip);
            if (node is not ComponentNode)
            {
                node.AddNodeFlags(NodeFlags.HasCollision);
            }
        }

        public ReadOnlySeString Text { get; set; }
    }
}
