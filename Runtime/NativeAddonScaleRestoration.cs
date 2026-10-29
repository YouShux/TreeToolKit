using System.Collections;
using System.Reflection;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.BaseTypes;

namespace TreeToolKit;

public sealed class NativeAddonScaleRestoration : IDisposable
{
    private readonly IDictionary              addonConfigs;
    private readonly FieldInfo                scaleField;
    private readonly IEnumerable<NativeAddon> nativeAddons;
    private readonly IAddonLifecycle          addonLifecycle;
    private readonly Func<string, bool>       ownsAddon;
    private bool                              disposed;

    private NativeAddonScaleRestoration
    (
        IDictionary              addonConfigs,
        FieldInfo                scaleField,
        IEnumerable<NativeAddon> nativeAddons,
        IAddonLifecycle          addonLifecycle,
        Func<string, bool>       ownsAddon
    )
    {
        this.addonConfigs   = addonConfigs;
        this.scaleField     = scaleField;
        this.nativeAddons   = nativeAddons;
        this.addonLifecycle = addonLifecycle;
        this.ownsAddon      = ownsAddon;
        addonLifecycle.RegisterListener(AddonEvent.PreSetup, OnPreSetup);
        try
        {
            addonLifecycle.RegisterListener(AddonEvent.PostSetup, OnPostSetup);
        }
        catch
        {
            addonLifecycle.UnregisterListener(OnPreSetup);
            throw;
        }
    }

    public static NativeAddonScaleRestoration? Create
    (
        IAddonLifecycle    addonLifecycle,
        Func<string, bool> ownsAddon,
        Action<Exception>? errorReporter = null
    )
    {
        ArgumentNullException.ThrowIfNull(addonLifecycle);
        ArgumentNullException.ThrowIfNull(ownsAddon);
        try
        {
            // 当前 Kami 未公开配置读取接口；复用其内存配置，保持原有保存与迁移链路。
            var configFileField = typeof(KamiToolKitLibrary).GetField("AddonConfigFile", BindingFlags.Static | BindingFlags.NonPublic)
                                  ?? throw new MissingFieldException(typeof(KamiToolKitLibrary).FullName, "AddonConfigFile");
            var configFile = configFileField.GetValue(null)
                             ?? throw new InvalidOperationException("KamiToolKit 窗口配置尚未初始化。");
            var configsField = configFile.GetType().GetField("AddonConfigs")
                               ?? throw new MissingFieldException(configFile.GetType().FullName, "AddonConfigs");
            var configs = configsField.GetValue(configFile) as IDictionary
                          ?? throw new InvalidOperationException("KamiToolKit 窗口配置集合不可用。");
            var configType = configsField.FieldType.GetGenericArguments()[1];
            var scale = configType.GetField("Scale")
                        ?? throw new MissingFieldException(configType.FullName, "Scale");
            var addonsProperty = typeof(NativeAddon).GetProperty("CreatedAddons", BindingFlags.Static | BindingFlags.NonPublic)
                                 ?? throw new MissingMemberException(typeof(NativeAddon).FullName, "CreatedAddons");
            var addons = addonsProperty.GetValue(null) as IEnumerable<NativeAddon>
                         ?? throw new InvalidOperationException("KamiToolKit 原生窗口列表尚未初始化。");
            return new(configs, scale, addons, addonLifecycle, ownsAddon);
        }
        catch (Exception exception)
        {
            errorReporter?.Invoke(exception);
            return null;
        }
    }

    private unsafe void OnPreSetup
    (
        AddonEvent type,
        AddonArgs  args
    )
    {
        if (disposed || !ownsAddon(args.AddonName) ||
            addonConfigs[args.AddonName] is not { } config)
            return;

        try
        {
            // Kami 的初始状态跳过 100%；先补回该值，随后窗口自身的贴附缩放仍然优先。
            if (scaleField.GetValue(config) is 1f)
                ((AtkUnitBase*)args.Addon.Address)->SetScale(1f, true);
        }
        catch (Exception)
        {
            Dispose();
        }
    }

    private unsafe void OnPostSetup
    (
        AddonEvent type,
        AddonArgs  args
    )
    {
        if (disposed || !ownsAddon(args.AddonName))
            return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        foreach (var window in nativeAddons)
        {
            if ((AtkUnitBase*)window != addon)
                continue;
            addon->EnableTitleBarContextMenu = window.EnableContextMenu;
            addon->DisableUserScaling        = window.DisableScaleContextOption;
            return;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        List<Exception>? errors = null;
        try
        {
            addonLifecycle.UnregisterListener(OnPreSetup);
        }
        catch (Exception exception)
        {
            (errors ??= []).Add(exception);
        }
        try
        {
            addonLifecycle.UnregisterListener(OnPostSetup);
        }
        catch (Exception exception)
        {
            (errors ??= []).Add(exception);
        }
        if (errors is not null)
            throw new AggregateException("TreeToolKit 缩放辅助事件解绑失败。", errors);
    }
}
