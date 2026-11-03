using System.Reflection;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Extensions;

namespace TreeToolKit;

public static unsafe class NativeNodeDetach
{
    private static readonly FieldInfo ChildNodesField = typeof(NodeBase).GetField("ChildNodes", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(NodeBase).FullName, "ChildNodes");

    public static bool DetachAndDestroy(NodeBase? node)
    {
        if (node is null)
            return false;

        AtkResNode* nativeNode = node;
        if (nativeNode is null)
            return false;

        ThreadSafety.AssertMainThread();
        node.HideTooltip();
        ClearInteractionState(nativeNode);
        DestroyManagedChildren(node);
        var parent = nativeNode->ParentNode;
        if (parent is not null && parent->GetNodeType() is not NodeType.Component &&
            parent->ChildNode != nativeNode && parent->ChildCount > 0)
            parent->ChildCount--;

        if (node is ComponentNode component)
        {
            // Deinitialize 仍会使用碰撞根；从托管递归和 ULD 对象表移交出来，保持其存活到组件析构结束。
            var collision = component.CollisionNode;
            AtkResNode* collisionNode = collision;
            collision.DetachNode();
            collisionNode->ParentNode = nativeNode;
            var children = (List<NodeBase>)ChildNodesField.GetValue(component)!;
            children.Remove(collision);
            component.Dispose();
            if ((AtkResNode*)component is not null)
            {
                children.Add(collision);
                throw new InvalidOperationException("KamiToolKit component disposal did not complete.");
            }

            // 组件已释放，碰撞根不再通过已失效的父节点、组件或 ULD 指针解绑。
            collisionNode->ParentNode = null;
            collision.LinkedComponent = null;
            collision.Dispose();
            if ((AtkResNode*)collision is not null)
                throw new InvalidOperationException("KamiToolKit collision root disposal did not complete.");
            return true;
        }

        node.DetachNode();
        nativeNode->Destroy(true);
        return true;
    }

    // 节点的原生 Destroy 不会释放 Component，使用保留内部根节点的完整组件释放路径。
    public static void DetachAndDestroyComponent(ComponentNode? node) => DetachAndDestroy(node);

    private static void DestroyManagedChildren(NodeBase node)
    {
        if (ChildNodesField.GetValue(node) is not List<NodeBase> children)
            throw new InvalidOperationException("KamiToolKit managed node ownership is unavailable.");

        foreach (var child in children.ToArray())
        {
            if (node is ComponentNode component && ReferenceEquals(child, component.CollisionNode))
                DestroyManagedChildren(child);
            else
                DetachAndDestroy(child);
        }
    }

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
