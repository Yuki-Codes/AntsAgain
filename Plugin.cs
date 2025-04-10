using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using static FFXIVClientStructs.FFXIV.Client.Game.ActionManager;

using XivAction = Lumina.Excel.Sheets.Action;

public sealed class DalamudPlugin : IDalamudPlugin
{
	[PluginService] public static IFramework Framework { get; private set; } = null!;
	[PluginService] public static IPluginLog Log { get; private set; } = null!;
	[PluginService] public static IGameInteropProvider InteropProvider { get; private set; } = null!;
	[PluginService] public static IDataManager DataManager { get; private set; } = null!;
	[PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
	[PluginService] public static ICondition Condition { get; private set; } = null!;

	private Config config;
	private ConfigWindow configWindow;

	private Hook<ActionManager.Delegates.IsActionHighlighted>? isHighlightedHook;
	private Hook<ActionManager.Delegates.UseAction>? useActionHook;

	private Dictionary<uint, DateTime?> castActions = new();
	private Dictionary<uint, List<uint>> actionMaps = new();

	public unsafe DalamudPlugin(IDalamudPluginInterface pluginInterface)
	{
		this.config = PluginInterface.GetPluginConfig() as Config ?? new Config();
		this.configWindow = new ConfigWindow(this.config);

		PluginInterface.UiBuilder.Draw += () => this.configWindow.Draw();

		PluginInterface.UiBuilder.OpenConfigUi += () => this.configWindow.IsOpen = true;

		this.isHighlightedHook = InteropProvider.HookFromAddress<ActionManager.Delegates.IsActionHighlighted>(ActionManager.Addresses.IsActionHighlighted.Value, this.IsActionHighlighted);
		this.isHighlightedHook.Enable();

		this.useActionHook = InteropProvider.HookFromAddress<ActionManager.Delegates.UseAction>(ActionManager.Addresses.UseAction.Value, this.UseAction);
		this.useActionHook.Enable();

		SubrowExcelSheet<ReplaceAction> actionReplacements = DataManager.GetSubrowExcelSheet<ReplaceAction>();
		foreach(SubrowCollection<ReplaceAction> actionReplacementGroup in actionReplacements)
		{
			foreach(ReplaceAction action in actionReplacementGroup)
			{
				foreach(RowRef<XivAction> replaceAction in action.ReplaceActions)
				{
					if (replaceAction.RowId == 0)
						continue;

					if (!actionMaps.ContainsKey(action.Action.RowId))
						actionMaps.Add(action.Action.RowId, new());

					actionMaps[action.Action.RowId].Add(replaceAction.RowId);
				}
			}
		}
	}

	public string Name => "Ants Again";

	public void Dispose()
	{
		this.isHighlightedHook?.Disable();
		this.isHighlightedHook?.Dispose();

		this.useActionHook?.Disable();
		this.useActionHook?.Dispose();
	}

	private bool ShouldHighlight(uint actionId)
	{
		if (!Condition[ConditionFlag.InCombat])
			return false;

		if (!this.castActions.TryGetValue(actionId, out var usedTime))
			return false;

		if (usedTime == null)
			return false;

		if (!this.config.ActionCooldowns.TryGetValue(actionId, out int cooldown))
			return false;

		TimeSpan? duration = Framework.LastUpdate - usedTime;
		if (duration == null)
			return false;

		return duration.Value.TotalSeconds > cooldown;
	}

	private unsafe bool IsActionHighlighted(ActionManager* self, ActionType actionType, uint actionId)
	{
		if (this.ShouldHighlight(actionId))
		{
			return true;
		}

		return this.isHighlightedHook?.Original(self, actionType, actionId) == true;
	}

	 private unsafe bool UseAction(ActionManager* self, ActionType actionType, uint actionId, ulong targetId = 0xE000_0000, uint extraParam = 0, UseActionMode mode = UseActionMode.None, uint comboRouteId = 0, bool* outOptAreaTargeted = null)
	 {
		if (this.config.ActionCooldowns.ContainsKey(actionId))
			this.castActions[actionId] = Framework.LastUpdate;

		if (actionMaps.TryGetValue(actionId, out List<uint>? actions) && actions != null)
		{
			foreach(uint newactionId in actions)
			{
				if (this.config.ActionCooldowns.ContainsKey(newactionId))
				{
					this.castActions[newactionId] = Framework.LastUpdate;
				}
			}
		}

		return 	this.useActionHook?.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted) == true;
	 }

}