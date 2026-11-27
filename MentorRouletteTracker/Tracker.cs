using System;
using System.Collections.Concurrent;
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

internal sealed class Tracker : IDisposable
{
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

	private readonly ConcurrentQueue<Said> _toSay = new();

	private readonly List<Said> _afterLogin = [];

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

		IsDev = pluginInterface.IsDev;
		switch (ServerAddress.OnLoad(Config.ServerAddress, IsDev))
		{
			case SavedAddressChange.Move:
				_log.Information("Moved to {Server} from the site's former address {Other}", ServerAddress.Default, Config.ServerAddress);
				Config.ServerAddress = ServerAddress.Default;
				SaveConfig();
				break;
			case SavedAddressChange.Reset:
				_log.Information("Went back to {Server} from {Other}, which only dev builds can use", ServerAddress.Default, Config.ServerAddress);
				Config.ServerAddress = ServerAddress.Default;
				Config.Token = null;
				Config.Username = null;
				SaveConfig();
				break;
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

	public bool RunsSaved()
	{
		return _runsSaver.Flush();
	}

	public void RunEnded(Run run)
	{
		var duty = Names.Duty(run.GameContentId);
		var outcome = Keeper.KeepAsync(run, _stopping.Token);

		Background(SayAsync, "upload the run that ended");

		async Task SayAsync()
		{
			var ended = await outcome;
			LogOutcome(ended);
			Say(ChatLine.Format(run, duty, ended, Config.ChatLinePace), afterLogin: false);
		}
	}

	private void LogOutcome(RunOutcome outcome)
	{
		switch (outcome.Kind)
		{
			case RunOutcomeKind.Logged:
				_log.Information("Uploaded run as #{RunNumber}", outcome.RunNumber.GetValueOrDefault());
				break;
			case RunOutcomeKind.Refused:
				_log.Warning("The site refused the run: {Reason}", outcome.Reason ?? "no reason given");
				break;
			case RunOutcomeKind.NeedsReconnect:
				_log.Warning("The site no longer accepts the plugin's connection, so uploads are paused until it's connected again");
				break;
			case RunOutcomeKind.Waiting:
				_log.Warning("Couldn't upload the run, so it'll be tried again later: {Problem}", Uploader.Problem ?? "the site couldn't be reached");
				break;
			case RunOutcomeKind.NotConnected:
				_log.Information("Not uploaded: the plugin isn't connected to the site");
				break;
			case RunOutcomeKind.AutoUploadOff:
				_log.Information("Not uploaded: auto-upload is off");
				break;
		}
	}

	public void EndMissed(uint gameContentId)
	{
		Say(ChatLine.FormatEndMissed(Names.Duty(gameContentId)), afterLogin: false, opensRuns: false);
	}

	private void OnWaitingRunsLogged(IReadOnlyList<LoggedRun> logged)
	{
		foreach (var run in logged)
		{
			_log.Debug("Uploaded a run that was waiting as #{RunNumber}", run.RunNumber);
		}

		Say(ChatLine.FormatWaiting(logged), afterLogin: true);
	}

	private void Say(string line, bool afterLogin, bool opensRuns = true)
	{
		if (_stopping.IsCancellationRequested)
		{
			return;
		}

		_toSay.Enqueue(new Said(line, afterLogin, opensRuns));
		Background(() => _framework.RunOnFrameworkThread(PrintSaid), "print a chat line");
	}

	private void PrintSaid()
	{
		while (_toSay.TryDequeue(out var said))
		{
			if (!Config.ChatLine || _stopping.IsCancellationRequested)
			{
				continue;
			}

			if (said.AfterLogin && !_clientState.IsLoggedIn)
			{
				_afterLogin.Add(said);
			}
			else
			{
				_chat.Print(ChatMessage(said));
			}
		}
	}

	private void OnLogin()
	{
		if (Config.ChatLine && !_stopping.IsCancellationRequested)
		{
			foreach (var said in _afterLogin)
			{
				_chat.Print(ChatMessage(said));
			}
		}

		_afterLogin.Clear();
	}

	private SeString ChatMessage(Said said)
	{
		if (!said.OpensRuns)
		{
			return new SeStringBuilder().AddUiForeground(said.Line, ChatColour).Build();
		}

		return new SeStringBuilder()
			.Add(_openRunsLink)
			.AddUiForeground(said.Line, ChatColour)
			.Add(RawPayload.LinkTerminator)
			.Build();
	}

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

	public void DeleteUploaded()
	{
		Background(() => Uploader.DeleteUploadedAsync(_stopping.Token), "delete the uploaded runs");
	}

	public void DeleteAll()
	{
		Background(() => Uploader.DeleteAllAsync(_stopping.Token), "delete the runs");
	}

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

	private void Background(Func<Task> work, string what)
	{
		Task.Run(work).ContinueWith(
			done => _log.Error(done.Exception!, "Couldn't {What}", what),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted,
			TaskScheduler.Default);
	}

	private sealed record Said(string Line, bool AfterLogin, bool OpensRuns);
}
