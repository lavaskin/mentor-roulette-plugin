using System;
using System.IO;
using Dalamud.Plugin;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker;

internal sealed class PluginFiles
{
	public const string DevFolderName = "dev";
	public const string DevSettingsFileName = "settings.json";

	private readonly IDalamudPluginInterface _pluginInterface;
	private readonly Action<string> _warn;
	private readonly string? _devSettingsPath;

	public PluginFiles(IDalamudPluginInterface pluginInterface, Action<string> warn)
	{
		_pluginInterface = pluginInterface;
		_warn = warn;

		var directory = pluginInterface.GetPluginConfigDirectory();
		if (pluginInterface.IsDev)
		{
			directory = Path.Combine(directory, DevFolderName);
			_devSettingsPath = Path.Combine(directory, DevSettingsFileName);
		}

		Runs = new RunStore(directory, warn);
	}

	public RunStore Runs { get; }

	public Configuration LoadSettings()
	{
		var settings = _devSettingsPath is null
			? _pluginInterface.GetPluginConfig() as Configuration
			: JsonFile.Read<Configuration>(_devSettingsPath, _warn);
		return settings ?? new Configuration();
	}

	public void SaveSettings(Configuration settings)
	{
		if (_devSettingsPath is null)
		{
			_pluginInterface.SavePluginConfig(settings);
		}
		else
		{
			JsonFile.Write(_devSettingsPath, settings);
		}
	}
}
