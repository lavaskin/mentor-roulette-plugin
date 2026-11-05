using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MentorRouletteTracker.Windows;

namespace MentorRouletteTracker;

public sealed class Plugin : IDalamudPlugin
{
	private const string Command = "/mrt";

	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

	private readonly WindowSystem _windowSystem = new("MentorRouletteTracker");
	private readonly RunsWindow _runsWindow = new();
	private readonly SettingsWindow _settingsWindow = new();

	public Plugin()
	{
		_windowSystem.AddWindow(_runsWindow);
		_windowSystem.AddWindow(_settingsWindow);

		CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
		{
			HelpMessage = $"Open your Mentor Roulette runs. {Command} config opens the settings.",
		});

		PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi += _runsWindow.Toggle;
		PluginInterface.UiBuilder.OpenConfigUi += _settingsWindow.Toggle;
	}

	public void Dispose()
	{
		PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
		PluginInterface.UiBuilder.OpenMainUi -= _runsWindow.Toggle;
		PluginInterface.UiBuilder.OpenConfigUi -= _settingsWindow.Toggle;

		CommandManager.RemoveHandler(Command);
		_windowSystem.RemoveAllWindows();
	}

	private void OnCommand(string command, string arguments)
	{
		if (arguments.Trim().Equals("config", StringComparison.OrdinalIgnoreCase))
		{
			_settingsWindow.Toggle();
		}
		else
		{
			_runsWindow.Toggle();
		}
	}
}
