using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Dalamud.Plugin;

namespace TreeToolKit;

public class KamiToolKitPluginInterface : DispatchProxy
{
    private IDalamudPluginInterface source          = null!;
    private DirectoryInfo           configDirectory = null!;

    public static IDalamudPluginInterface Create
    (
        IDalamudPluginInterface source,
        string                  configDirectory
    )
    {
        var proxy = Create<IDalamudPluginInterface, KamiToolKitPluginInterface>();
        var instance = (KamiToolKitPluginInterface)proxy;
        instance.source          = source;
        instance.configDirectory = new(configDirectory);
        return proxy;
    }

    protected override object? Invoke
    (
        MethodInfo? method,
        object?[]?  args
    )
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        if (method.Name == "get_ConfigDirectory") return configDirectory;
        if (method.Name == nameof(IDalamudPluginInterface.GetPluginConfigDirectory)) return configDirectory.FullName;
        try
        {
            return method.Invoke(source, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
