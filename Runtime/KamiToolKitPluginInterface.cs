using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;

namespace TreeToolKit;

public class KamiToolKitPluginInterface : DispatchProxy
{
    private static readonly DefaultJsonTypeInfoResolver ConfigResolver = new();
    private IDalamudPluginInterface source          = null!;
    private DirectoryInfo           configDirectory = null!;

    public static Task DisposeAsync(IFramework framework, IPluginLog log) => KamiToolKitLibrary.DisposeAsync();

    public static IDalamudPluginInterface Create
    (
        IDalamudPluginInterface source,
        string                  configDirectory
    )
    {
        var configType = typeof(KamiToolKitLibrary).Assembly.GetType("KamiToolKit.Classes.AddonConfigFile", true)!;
        var optionsField = configType.GetField("SerializerOptions", BindingFlags.Static | BindingFlags.NonPublic);
        if (optionsField?.GetValue(null) is not JsonSerializerOptions options)
            throw new InvalidOperationException("KamiToolKit JSON options must be prepared before initialization.");
        // 独立 resolver 防止等价选项复用其他插件长期持有的类型缓存。
        if (!ReferenceEquals(options.TypeInfoResolver, ConfigResolver))
        {
            if (options.IsReadOnly)
                throw new InvalidOperationException("KamiToolKit JSON options must be prepared before initialization.");
            options.TypeInfoResolver = ConfigResolver;
        }
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
