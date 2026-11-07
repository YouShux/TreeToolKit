using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.MapOverlay;
using OmenTools;
using TreeTools.Lifecycle;

namespace TreeToolKit;

public sealed unsafe class NativeMapOverlayController : IDisposable
{
    private readonly MapOverlayController controller       = new();
    private readonly AddonEventRegistry   addonEvents;
    private readonly IKeyState            keyState;
    private bool                          disposed;
    private bool                          requestedVisible = true;

    public NativeMapOverlayController(IKeyState keyState)
    {
        this.keyState = keyState ?? throw new ArgumentNullException(nameof(keyState));
        addonEvents   = new(DService.Instance().AddonLifecycle);
        foreach (var addonName in MAP_ADDON_NAMES)
            addonEvents.Register(AddonEvent.PreUpdate, addonName, OnMapPreUpdate);
    }

    public VirtualKey? HideKey { get; set; } = VirtualKey.CONTROL;

    public bool IsVisible
    {
        get => requestedVisible;
        set
        {
            requestedVisible     = value;
            controller.IsVisible = value && !IsHideKeyPressed();
        }
    }

    public void Enable() => controller.Enable();

    public void Disable() => controller.Disable();

    public void AddMarker(MapMarkerInfo markerInfo) => controller.AddMarker(markerInfo);

    public void AddMarker(MapMarkerNode marker) => controller.AddMarker(marker);

    public void RemoveMarker(MapMarkerNode marker) => controller.RemoveMarker(marker);

    public void Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var hidden = IsHideKeyPressed();
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        if (agent is not null && HideKey != VirtualKey.CONTROL && agent->IsControlKeyPressed)
            agent->IsControlKeyPressed = false;
        controller.IsVisible = requestedVisible && !hidden;
        if (!hidden)
            return;

        var manager = RaptureAtkUnitManager.Instance();
        var addon = manager is null ? null : manager->GetAddonByName("AreaMap");
        var stage = AtkStage.Instance();
        if (addon is not null && stage is not null)
            stage->TooltipManager.HideTooltip(addon->Id);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            controller.Dispose();
        }
        finally
        {
            addonEvents.Dispose();
        }
    }

    private bool IsHideKeyPressed() => HideKey is { } key && keyState[key];

    private void OnMapPreUpdate(AddonEvent _, AddonArgs args)
    {
        if (HideKey == VirtualKey.CONTROL)
            return;

        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        if (agent is not null)
            agent->IsControlKeyPressed = false;
    }

    #region 常量

    private static readonly string[] MAP_ADDON_NAMES = ["_NaviMap", "AreaMap", "TeleportTown"];

    #endregion
}
