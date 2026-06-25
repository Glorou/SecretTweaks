using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.Shell;
using SimpleTweaksPlugin;
using SimpleTweaksPlugin.Events;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SecretTweaks;

[TweakName("Change Focus Targets Name Color")]
[TweakDescription("Changes your focus targets name color")]

[TweakAutoConfig]
public class ChangeColor : SecretTweaks.SubTweak
{
    public class ColorConfig : TweakConfig
    {
        public Vector4 Text = new Vector4(203, 0, 255, 255);
        public Vector4 Glow = new Vector4(203, 0, 255, 255);
    }

    private unsafe GameObject* Focused;
    private unsafe delegate GameObject.NamePlateColors GetNameplateColorDelegate(GameObject* self);

    [TweakHook, Signature("E8 ?? ?? ?? ?? 48 89 43 FB", DetourName = nameof(NameplateColorDetour))]
    private HookWrapper<GetNameplateColorDelegate> getNameplate;

    protected void DrawConfig(ref bool hasChanged)
    {
        var overrideText = Config?.Text ?? new Vector4(203, 0, 255, 255);
        var overrideGlow = Config?.Glow ?? new Vector4(203, 0, 255, 255);
        Config?.Text = ImGuiComponents.ColorPickerWithPalette(0, "Text Color", overrideText, ImGuiColorEditFlags.NoAlpha);
        ImGui.SameLine();
        ImGui.Text("Text Color");
        Config?.Glow = ImGuiComponents.ColorPickerWithPalette(1, "Glow Color", overrideGlow, ImGuiColorEditFlags.NoAlpha);
        ImGui.SameLine();
        ImGui.Text("Glow Color");

        if (!overrideGlow.Equals(Config?.Glow) || !overrideText.Equals(Config?.Text))
        {
            hasChanged = true;
            Service.NamePlateGui.RequestRedraw();
        }
            
    }
    
    protected override void Enable()
    {
        Service.NamePlateGui.OnNamePlateUpdate += HandlePlateUpdate;
        Service.NamePlateGui.RequestRedraw();
        base.Enable();
    }

    protected override void Disable()
    {
        Service.NamePlateGui.OnNamePlateUpdate -= HandlePlateUpdate;
        Service.NamePlateGui.RequestRedraw();
        base.Disable();
    }

    [TweakConfig] public ColorConfig Config { get; private set; }

    [FrameworkUpdate(NthTick = 10)]
    private unsafe void UpdatePlate()
    {
        if (TargetSystem.Instance()->FocusTarget != Focused)
        {
            Focused = TargetSystem.Instance()->FocusTarget;
            Service.NamePlateGui.RequestRedraw();
        }
    }
    private unsafe GameObject.NamePlateColors NameplateColorDetour(GameObject* self)
    {
        var plateColors = this.getNameplate.Original(self);
        if (TargetSystem.Instance()->FocusTarget == self)
        {
            plateColors.Color.RGBA = MakeOpaque(Dalamud.Utility.Numerics.VectorExtensions.ToByteColor(Config.Text).RGBA);
            plateColors.EdgeColor.RGBA = MakeOpaque(Dalamud.Utility.Numerics.VectorExtensions.ToByteColor(Config.Glow).RGBA);
        }

        return plateColors;
    }
    private unsafe void HandlePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        foreach (var handle in handlers)
        {
            if(handle.NamePlateKind != NamePlateKind.PlayerCharacter)
                continue;
            var addr = handle.PlayerCharacter?.Address ?? nint.Zero;
            if (addr == nint.Zero)
                continue;
            if (handle.PlayerCharacter?.ObjectIndex is null or 0) continue;


            if (TargetSystem.Instance()->FocusTarget->ObjectIndex == handle.PlayerCharacter.ObjectIndex)
            {
                NamePlateNumberArray.Instance()->ObjectData[handle.ArrayIndex].GaugeFillColor = MakeOpaque(Dalamud.Utility.Numerics.VectorExtensions.ToByteColor(Config.Text).RGBA);
                handle.TextColor = MakeOpaque(Dalamud.Utility.Numerics.VectorExtensions.ToByteColor(Config.Text).RGBA);
                handle.EdgeColor = MakeOpaque(Dalamud.Utility.Numerics.VectorExtensions.ToByteColor(Config.Glow).RGBA); // figure out arrow and hp bar
            }
        }
    }
    
    private static uint MakeOpaque(uint rgb)
    {
        if (rgb == 0) return 0;
        return (rgb & 0x00FFFFFF) | 0xFF000000; // ensure AA = 0xFF
    }
}