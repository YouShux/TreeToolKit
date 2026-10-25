using KamiToolKit.UiOverlay;

namespace TreeToolKit;

public sealed class NativeOverlayController : IDisposable
{
    private static OverlayController? SharedController;
    private static int OwnerCount;

    private readonly OverlayController controller;
    private readonly List<OverlayNode> nodes = [];
    private bool disposed;

    public NativeOverlayController()
    {
        controller = SharedController ??= new();
        OwnerCount++;
    }

    public void AddNode(OverlayNode node)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (nodes.Contains(node))
            return;

        controller.AddNode(node);
        nodes.Add(node);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        List<Exception>? errors = null;
        foreach (var node in nodes)
        {
            try
            {
                controller.RemoveNode(node);
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }
        nodes.Clear();
        OwnerCount--;
        if (OwnerCount == 0)
        {
            SharedController = null;
            try
            {
                controller.Dispose();
            }
            catch (Exception exception)
            {
                (errors ??= []).Add(exception);
            }
        }
        if (errors is not null)
            throw new AggregateException("TreeToolKit 浮层节点释放失败。", errors);
    }
}
