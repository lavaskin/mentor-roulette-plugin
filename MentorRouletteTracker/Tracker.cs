using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;
using MentorRouletteTracker.Core.Text;
using MentorRouletteTracker.Core.Uploading;

namespace MentorRouletteTracker;

/// <summary>
/// The plugin's runs, connection and uploads, as the windows and the run watcher use them. Network calls and file
/// writes run off the framework thread; settings change on it.
/// </summary>
internal sealed class Tracker : IDisposable
{
	/// <summary>
	/// The game's <c>UIColor</c> row for the chat line: a soft gold (#F3CC64, and a darker gold on light themes)
	/// </summary>
	private const ushort ChatColour = 31;

	private const uint OpenRunsLinkId = 1;

	private readonly IFramework _framework;
	private readonly IClientState _clientState;
	private readonly IChatGui _chat;
	private readonly IPluginLog _log;
	private readonly IMentorApi _api;
	private readonly BackgroundSaver _runsSaver;
	private readonly BackgroundSaver _configSaver;
	private readonly CancellationTokenSource _stopping = new();
	private readonly DalamudLinkPayload _openRunsLink;

	/// <summary>
	/// Chat lines waiting for the player to log in, on the framework thread
	/// </summary>
	private readonly List<string> _afterLogin = [];

	public Tracker(
		IDalamudPluginInterface pluginInterface,
		IFramework framework,
		IClientState clientState,
		IChatGui chat,
		IPluginLog log,
		GameNames names,
		PluginFiles files,
		IMentorApi api,
		Action openRuns)
	{
		_framework = framework;
		_clientState = clientState;
		_chat = chat;
		_log = log;
		_api = api;
		Names = names;
		_openRunsLink = chat.AddChatLinkHandler(OpenRunsLinkId, (_, _) => openRuns());

		Config = files.LoadSettings();
		_configSaver = new BackgroundSaver(
			() => files.SaveSettings(Config),
			exception => _log.Error(exception, "Couldn't save the settings"));

		// Only a dev build can point at another site. Dev builds keep their own settings, but one from 1.0.0 shared
		// the installed plugin's, so the installed one goes back to the real site, dropping the token, which was for
		// the other one.
		IsDev = pluginInterface.IsDev;
		if (!IsDev && Config.ServerAddress != ServerAddress.Default)
		{
			_log.Information("Went back to {Server} from {Other}, which only dev builds can use", ServerAddress.Default, Config.ServerAddress);
			Config.ServerAddress = ServerAddress.Default;
			Config.Token = null;
			Config.Username = null;
			SaveConfig();
		}

		Log = new RunLog(files.Runs.LoadRuns());
		_runsSaver = new BackgroundSaver(
			() => files.Runs.SaveRuns(Log.Runs),
			exception => _log.Error(exception, "Couldn't save the runs"));
		Log.Changed += _runsSaver.Request;

		Uploader = new Uploader(Log, api, () => RunKeeper.ConnectionFor(Config));
		Keeper = new RunKeeper(Log, Uploader, Config);
		Keeper.WaitingRunsLogged += OnWaitingRunsLogged;
		_clientState.Login += OnLogin;

		Background(() => Keeper.ResumeAsync(_stopping.Token), "upload the runs waiting");
	}

	public Configuration Config { get; }
	public RunLog Log { get; }
	public Uploader Uploader { get; }
	public RunKeeper Keeper { get; }
	public GameNames Names { get; }

	/// <summary>
	/// Loaded as a dev plugin (from Dev Plugin Locations), not installed from the plugin's repository
	/// </summary>
	public bool IsDev { get; }

	public bool IsConnected => RunKeeper.ConnectionFor(Config) is not null;

	public void Dispose()
	{
		_stopping.Cancel();
		_clientState.Login -= OnLogin;
		Keeper.WaitingRunsLogged -= OnWaitingRunsLogged;
		_chat.RemoveChatLinkHandler();
		Log.Changed -= _runsSaver.Request;
		_runsSaver.Flush();
		_configSaver.Flush();
	}

	public void SaveConfig()
	{
		_configSaver.Request();
	}

	/// <summary>
	/// Waits for the runs to be saved, off the framework thread
	/// </summary>
	/// <returns>Whether the last save worked</returns>
	public bool RunsSaved()
	{
		return _runsSaver.Flush();
	}

	/// <summary>
	/// A run that just ended, on the framework thread: kept now, then uploaded if auto-upload's on, then the chat line
	/// </summary>
	public void RunEnded(Run run)
	{
		var duty = Names.Duty(run.GameContentId);
		var outcome = Keeper.KeepAsync(run, _stopping.Token);

		Background(SayAsync, "upload the run that ended");

		async Task SayAsync()
		{
			var line = ChatLine.Format(run, duty, await outcome);
			if (Config.ChatLine && !_stopping.IsCancellationRequested)
			{
				await _framework.RunOnFrameworkThread(() => _chat.Print(ChatMessage(line)));
			}
		}
	}

	/// <summary>
	/// Runs that were waiting were logged, on the thread that uploaded them: the chat line, once the player's logged in,
	/// since the plugin starts at the title screen and uploads straight away
	/// </summary>
	private void OnWaitingRunsLogged(IReadOnlyList<LoggedRun> logged)
	{
		var line = ChatLine.FormatWaiting(logged);
		Background(() => _framework.RunOnFrameworkThread(() =>
		{
			if (!Config.ChatLine || _stopping.IsCancellationRequested)
			{
				return;
			}

			if (_clientState.IsLoggedIn)
			{
				_chat.Print(ChatMessage(line));
			}
			else
			{
				_afterLogin.Add(line);
			}
		}), "say which runs that were waiting were logged");
	}

	private void OnLogin()
	{
		foreach (var line in _afterLogin)
		{
			_chat.Print(ChatMessage(line));
		}

		_afterLogin.Clear();
	}

	/// <summary>
	/// The chat line (with its [MRT] tag) in gold, and clickable: clicking it opens the runs window
	/// </summary>
	private SeString ChatMessage(string line)
	{
		return new SeStringBuilder()
			.Add(_openRunsLink)
			.AddUiForeground(line, ChatColour)
			.Add(RawPayload.LinkTerminator)
			.Build();
	}

	/// <summary>
	/// "Upload N runs": every run that isn't uploaded, oldest first
	/// </summary>
	public void UploadAll()
	{
		Background(() => Uploader.UploadAsync(_stopping.Token), "upload the runs");
	}

	public void Retry(Guid id)
	{
		Background(() => Uploader.RetryAsync(id, _stopping.Token), "retry a run");
	}

	public void Delete(Guid id)
	{
		Background(() => Uploader.DeleteAsync(id, _stopping.Token), "delete a run");
	}

	/// <summary>
	/// Checks a token with the site, and keeps it if the site accepts it
	/// </summary>
	/// <returns>The site's answer, on the framework thread with the settings already changed</returns>
	public async Task<ApiResult<string>> ConnectAsync(string token)
	{
		var server = Config.ServerAddress;
		var result = await Task.Run(() => _api.GetUsernameAsync(new ApiConnection(server, token), _stopping.Token));
		await _framework.RunOnFrameworkThread(() =>
		{
			if (result.Kind == ApiResultKind.Ok)
			{
				Config.Token = token;
				Config.Username = result.Value;
				Uploader.ConnectionChanged();
				SaveConfig();
			}
		});
		return result;
	}

	public void Disconnect()
	{
		Config.Token = null;
		Config.Username = null;
		Uploader.ConnectionChanged();
		SaveConfig();
	}

	/// <summary>
	/// Runs <paramref name="work"/> on the thread pool, so even the start of a request stays off the framework thread
	/// </summary>
	private void Background(Func<Task> work, string what)
	{
		Task.Run(work).ContinueWith(
			done => _log.Error(done.Exception!, "Couldn't {What}", what),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}
}
