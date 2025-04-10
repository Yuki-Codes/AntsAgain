using System.Collections.Generic;
using Dalamud.Configuration;

public class Config : IPluginConfiguration
{
	public int Version { get; set; } = 1;

	public Dictionary<uint, int> ActionCooldowns { get; private set; } = new();
}