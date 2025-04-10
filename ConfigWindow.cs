using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using XivAction = Lumina.Excel.Sheets.Action;

public class ConfigWindow
{
	public readonly Config Config;
	public bool IsOpen = false;

	private ExcelSheet<XivAction> allActions;
	private ExcelSheet<ClassJob> allJobs;
	private int selectedAction = -1;
	private int selectedJob = -1;

	public ConfigWindow(Config config)
	{
		this.Config = config;

		this.allJobs =	DalamudPlugin.DataManager.GetExcelSheet<ClassJob>();
		this.allActions = DalamudPlugin.DataManager.GetExcelSheet<XivAction>();
	}

    public void Draw()
	{
		if (!this.IsOpen)
            return;

		if (ImGui.Begin("Ants Again", ref this.IsOpen))
		{
			List<string> jobNames = new();
			List<ClassJob?> jobs = new();

			jobNames.Add("Roles");
			jobs.Add(null);

			foreach(ClassJob job in allJobs)
			{
				jobs.Add(job);
				jobNames.Add(job.NameEnglish.ToString());
			}

			ImGui.Combo("Job", ref this.selectedJob, jobNames.ToArray(), jobNames.Count);

			if (this.selectedJob >= 0 && this.selectedJob < jobs.Count)
			{
				List<XivAction> actions = new();
				List<string> actionNames = new();
				ClassJob? selectedJob = jobs[this.selectedJob];

				foreach(XivAction xivAction in allActions)
				{
					if (!xivAction.IsPlayerAction)
						continue;

					if (selectedJob == null)
					{
						 if (!xivAction.IsRoleAction || xivAction.ClassJobLevel <= 0)
						 {
						 	continue;
						 }
					}
					else
					{
						if (xivAction.ClassJob.RowId != selectedJob.Value.RowId)
						{
							continue;
						}
					}

					actionNames.Add($"{xivAction.RowId} {xivAction.Name}");
					actions.Add(xivAction);
				}


				ImGui.Combo("Action", ref this.selectedAction, actionNames.ToArray(), actionNames.Count);

				if (this.selectedAction >= 0 && this.selectedAction < actions.Count)
				{
					if (ImGui.Button("Add"))
					{
						this.Config.ActionCooldowns[actions[this.selectedAction].RowId] = 1;
					}
				}
			}

			ImGui.Separator();
			uint toRemove = 0;
			foreach((uint actionId, int cooldown) in this.Config.ActionCooldowns)
			{
				ImGui.BeginChild($"AntsAgainActions_{actionId}", new Vector2(-1, 42));
				if (ImGui.Button("Remove"))
				{
					toRemove = actionId;
				}

				ImGui.SameLine();

				XivAction action = this.allActions.GetRow(actionId);

				GameIconLookup lookup = new GameIconLookup(action.Icon);
				IDalamudTextureWrap? tw = DalamudPlugin.TextureProvider.GetFromGameIcon(lookup).GetWrapOrDefault();
				if(tw != null)
					ImGui.Image(tw.ImGuiHandle, new Vector2(32, 32));

				ImGui.SameLine();
				ImGui.Text($"{action.Name} ({actionId})");

				ImGui.SameLine();
				int cd = cooldown;
				ImGui.SetNextItemWidth(100);
				ImGui.InputInt("Cooldown (seconds)", ref cd);
				this.Config.ActionCooldowns[actionId] = cd;

				ImGui.Separator();
				ImGui.EndChild();
			}

			if (toRemove != 0)
			{
				this.Config.ActionCooldowns.Remove(toRemove);
			}
		}

		ImGui.End();

		DalamudPlugin.PluginInterface.SavePluginConfig(this.Config);
	}
}