using System;
using System.Net.Http;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Storage;
using MentorRouletteTracker.Windows;

namespace MentorRouletteTracker;

public sealed class Plugin : IDalamudPlugin
{
	private const string Command = "/mrt";

	[PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
	[PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
	[PluginService] internal static IClientState ClientState { get; private set; } = null!;
	[PluginService] internal static IDutyState DutyState { get; private set; } = null!;
	[PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
	[PluginService] internal static IFramework Framework { get; private set; } = null!;
	[PluginService] internal static ICondition Condition { get; private set; } = null!;
	[PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
	[PluginService] internal static IDataManager DataManager { get; private set; } = null!;
	[PluginService] internal static IPluginLog Log { get; private set; } = null!;

	private readonly HttpClient _http;
	private readonly Tracker _tracker;
	private readonly RunWatcher _watcher;
	private readonly WindowSystem _windowSystem = new("MentorRouletteTracker");
	private readonly RunsWindow _runsWindow;
	private readonly SettingsWindow _settingsWindow;

	public Plugin()
	{
		var version = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
		_http = MentorApiClient.CreateHttpClient(version);

		var store = new RunStore(PluginInterface.GetPluginConfigDirectory(), warning => Log.Warning(warning));
		_tracker = new Tracker(
			PluginInterface,
			Framework,
			ChatGui,
			Log,
			new GameNames(DataManager),
			store,
			new MentorApiClient(_http),
			OpenRuns);
		_watcher = new RunWatcher(ClientState, DutyState, PlayerState, Framework, Condition, Log, store, _tracker.RunEnded, _tracker.RunsSaved);

		_runsWindow = new RunsWindow(_tracker, () => _settingsWindow!.IsOpen = true);
		_settingsWindow = new SettingsWindow(_tracker, () => _runsWindow.IsOpen = true);
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

		_watcher.Dispose();
		_tracker.Dispose();
		_http.Dispose();
	}

	/// <summary>
	/// Clicking the chat line: opens the runs window, or brings it to the front if it's open
	/// </summary>
	private void OpenRuns()
	{
		_runsWindow.IsOpen = true;
		_runsWindow.BringToFront();
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
