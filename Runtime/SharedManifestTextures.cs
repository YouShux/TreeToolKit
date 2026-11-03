using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin.Services;

namespace TreeToolKit;

public static class SharedManifestTextures
{
    /// <summary>全部绘制入口停止后，移除本加载上下文的内嵌纹理缓存并释放缓存自身的引用。</summary>
    public static int Release(ITextureProvider provider, Assembly owner)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(owner);
        var context = AssemblyLoadContext.GetLoadContext(owner);
        if (context is not { IsCollectible: true })
            throw new InvalidOperationException("The texture owner must belong to a collectible load context.");
        var host = typeof(ITextureProvider).Assembly;
        var providerType = provider.GetType();
        if (providerType.Assembly != host || providerType.FullName != "Dalamud.Interface.Textures.Internal.TextureManagerPluginScoped")
            throw new InvalidOperationException("The texture provider contract has changed.");
        var taskField = providerType.GetField("managerTaskNullable", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(providerType.FullName, "managerTaskNullable");
        if (taskField.GetValue(provider) is not Task task || !task.IsCompletedSuccessfully)
            return 0;
        var manager = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var shared = manager.GetType().GetField("sharedTextureManager", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(manager)
            ?? throw new MissingFieldException(manager.GetType().FullName, "sharedTextureManager");
        var dictionary = shared.GetType().GetField("manifestResourceDict", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(shared)
            ?? throw new MissingFieldException(shared.GetType().FullName, "manifestResourceDict");
        var shape = dictionary.GetType();
        if (!shape.IsGenericType || shape.GetGenericTypeDefinition() != typeof(ConcurrentDictionary<,>) ||
            shape.GenericTypeArguments[0] != typeof(ValueTuple<Assembly, string>))
            throw new InvalidOperationException("The manifest texture cache contract has changed.");
        var textureType = shape.GenericTypeArguments[1];
        if (textureType.Assembly != host || textureType.FullName != "Dalamud.Interface.Textures.Internal.SharedImmediateTextures.SharedImmediateTexture")
            throw new InvalidOperationException("The shared texture contract has changed.");
        var release = textureType.GetMethod("ReleaseSelfReference", BindingFlags.Instance | BindingFlags.Public, [typeof(bool)]);
        if (release is null || release.ReturnType != typeof(int))
            throw new MissingMethodException(textureType.FullName, "ReleaseSelfReference");
        return (int)typeof(SharedManifestTextures).GetMethod(nameof(ReleaseEntries), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(textureType).Invoke(null, [dictionary, context, release])!;
    }

    private static int ReleaseEntries<T>(object value, AssemblyLoadContext owner, MethodInfo release)
    {
        var dictionary = (ConcurrentDictionary<(Assembly Assembly, string Name), T>)value;
        var removed = 0;
        foreach (var pair in dictionary.ToArray())
        {
            if (AssemblyLoadContext.GetLoadContext(pair.Key.Assembly) != owner || !dictionary.TryRemove(pair)) continue;
            // 沿用宿主引用计数；RentAsync 等借用者仍拥有各自的引用，不强制 Dispose 纹理。
            release.Invoke(pair.Value, [true]);
            removed++;
        }
        return removed;
    }
}
