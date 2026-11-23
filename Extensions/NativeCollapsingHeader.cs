using System.Reflection;
using Dalamud.Utility;
using KamiToolKit.Nodes;

namespace TreeToolKit;

/// <summary>同步折叠标题的实际状态与布局。</summary>
public static class NativeCollapsingHeader
{
    private static readonly PropertyInfo HeaderProperty =
        typeof(CollapsingHeaderNode).GetProperty("ToggleableHeaderNode", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(CollapsingHeaderNode).FullName, "ToggleableHeaderNode");
    private static readonly PropertyInfo CollapsedProperty =
        HeaderProperty.PropertyType.GetProperty(nameof(CollapsingHeaderNode.IsCollapsed))
        ?? throw new MissingMemberException(HeaderProperty.PropertyType.FullName, nameof(CollapsingHeaderNode.IsCollapsed));

    /// <summary>在框架线程设置折叠状态，并沿用上游的箭头、布局与切换回调。</summary>
    public static void SetCollapsed(this CollapsingHeaderNode node, bool collapsed)
    {
        ThreadSafety.AssertMainThread();
        // 上游公开 Collapse/Uncollapse 未同步状态；内部属性与鼠标入口共用同一更新路径。
        var header = HeaderProperty.GetValue(node)
            ?? throw new InvalidOperationException("The collapsing header has no toggleable header node.");
        CollapsedProperty.SetValue(header, collapsed);
    }
}
