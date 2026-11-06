using System;
using System.IO;
using Dalamud.Plugin;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker;

/// <summary>
/// Where the plugin keeps its settings and runs. Dalamud gives a dev build the same config file and directory as the
/// installed plugin, so a dev build keeps its own in the directory's <c>dev</c> folder instead: <c>settings.json</c>
/// beside its runs. Trying a build then never touches the installed plugin's runs, and each keeps its own server and
/// token.
/// </summary>
internal sealed class PluginFiles
{
	public const string DevFolderName = "dev";
	public const string DevSettingsFileName = "settings.json";

	private readonly IDalamudPluginInterface _pluginInterface;
	private readonly Action<string> _warn;
	private readonly string? _devSettingsPath;

	/// <param name="warn">Told about a file that couldn't be read, which is set aside rather than overwritten</param>
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
