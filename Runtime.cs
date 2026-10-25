using Dalamud.Interface;
using Dalamud.Plugin.Services;
using TreeTools.Lifecycle;

namespace TreeToolKit;

/// <summary>持有本库字体及原生窗口辅助资源，不接管 KamiToolKit 的运行生命周期。</summary>
public sealed class Runtime : IAsyncDisposable
{
    private readonly FeatureLifetime lifetime = new();
    private readonly IFramework framework;

    private Runtime(IFramework framework) => this.framework = framework;

    /// <summary>在游戏线程初始化本库资源，由调用方提供服务与所拥有窗口的判定。</summary>
    public static Runtime Initialize(IFramework framework, IUiBuilder uiBuilder, IAddonLifecycle addonLifecycle,
        Func<string, bool> ownsAddon, Action<Exception>? errorReporter = null)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(uiBuilder);
        ArgumentNullException.ThrowIfNull(addonLifecycle);
        ArgumentNullException.ThrowIfNull(ownsAddon);
        var runtime = new Runtime(framework);
        try
        {
            Fonts.Initialize(uiBuilder);
            runtime.lifetime.Add(Fonts.Shutdown);
            var restoration = NativeAddonScaleRestoration.Create(addonLifecycle, ownsAddon, errorReporter);
            if (restoration is not null)
                runtime.lifetime.Add(restoration);
            return runtime;
        }
        catch (Exception initializationError)
        {
            try
            {
                runtime.lifetime.Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(initializationError, cleanupError);
            }
            throw;
        }
    }

    /// <summary>在游戏线程释放本库资源，重复调用等待同一个退出任务。</summary>
    public ValueTask DisposeAsync() => new(lifetime.DisposeAsync(
        action => framework.Run(action, cancellationToken: CancellationToken.None)));
}
