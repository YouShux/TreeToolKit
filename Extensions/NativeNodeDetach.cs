using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Extensions;

namespace TreeToolKit;

public static unsafe class NativeNodeDetach
{
    public static bool DetachAndDestroy(NodeBase? node)
    {
        if (node is null)
            return false;

        AtkResNode* nativeNode = node;
        if (nativeNode is null)
            return false;

        node.HideTooltip();
        ClearInteractionState(nativeNode);
        var parent = nativeNode->ParentNode;
        if (parent is not null && parent->GetNodeType() is not NodeType.Component &&
            parent->ChildNode != nativeNode && parent->ChildCount > 0)
            parent->ChildCount--;

        node.DetachNode();
        nativeNode->Destroy(true);
        return true;
    }

    // 组件内部根节点由原生 UldManager 终结；托管递归释放会过早销毁它。
    public static void DetachAndDestroyComponent(ComponentNode? node) => DetachAndDestroy(node);

    // KamiToolKit 拆挂非首子节点时不会减少非组件父节点的 ChildCount，累积后游戏会按错误计数遍历节点链表。
    public static bool DetachAndDispose(NodeBase? node)
    {
        if (node is null)
        {
            return false;
        }

        AtkResNode* nativeNode = node;
        if (nativeNode != null)
        {
            // 碰撞管理器缓存的悬停节点不会在节点释放时自动清除，先清掉避免同帧读到已释放节点。
            var collisionManager = AtkStage.Instance()->AtkCollisionManager;
            if (collisionManager != null &&
                (AtkResNode*)collisionManager->IntersectingCollisionNode == nativeNode)
            {
                collisionManager->IntersectingCollisionNode = null;
                collisionManager->IntersectingAddon         = null;
            }

            var parent = nativeNode->ParentNode;
            if (parent != null &&
                parent->GetNodeType() is not NodeType.Component &&
                parent->ChildNode != nativeNode &&
                parent->ChildCount > 0)
            {
                parent->ChildCount--;
            }
        }

        node.Dispose();
        return nativeNode != null;
    }

    private static void ClearInteractionState(AtkResNode* nativeNode)
    {
        var collisionManager = AtkStage.Instance()->AtkCollisionManager;
        if (collisionManager is not null)
        {
            var hovered = (AtkResNode*)collisionManager->IntersectingCollisionNode;
            while (hovered is not null && hovered != nativeNode)
                hovered = hovered->ParentNode;
            if (hovered is not null)
            {
                collisionManager->IntersectingCollisionNode = null;
                collisionManager->IntersectingAddon         = null;
            }
        }

        var stage = AtkStage.Instance();
        var focused = stage->AtkInputManager->FocusedNode;
        var ancestor = focused;
        while (ancestor is not null && ancestor != nativeNode)
            ancestor = ancestor->ParentNode;
        if (ancestor is not null)
            stage->ClearNodeFocus(focused);
        stage->ClearNodeFocus(nativeNode);
    }

    // KamiToolKit 的列表更新排队到下一帧，拆卸后同一帧的 RequestedUpdate 仍会遍历到已释放的碰撞节点，这里同步重建。
    public static void UpdateNodeLists(AtkUnitBase* addon)
    {
        if (addon == null)
        {
            return;
        }

        addon->UldManager.UpdateDrawNodeList();
        addon->UpdateCollisionNodeList(false);
    }
}
