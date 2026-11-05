using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using MentorRouletteTracker.Core;
using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker;

/// <summary>
/// The plugin's runs, connection and uploads, as the windows and the run watcher use them. Network calls and file
/// writes run off the framework thread; settings change on it.
/// </summary>
internal sealed class Tracker : IDisposable
{
	private readonly IDalamudPluginInterface _pluginInterface;
	private readonly IFramework _framework;
	private readonly IChatGui _chat;
	private readonly IPluginLog _log;
	private readonly IMentorApi _api;
	private readonly BackgroundSaver _runsSaver;
	private readonly BackgroundSaver _configSaver;
	private readonly CancellationTokenSource _stopping = new();

	public Tracker(
		IDalamudPluginInterface pluginInterface,
		IFramework framework,
		IChatGui chat,
		IPluginLog log,
		GameNames names,
		RunStore store,
		IMentorApi api)
	{
		_pluginInterface = pluginInterface;
		_framework = framework;
		_chat = chat;
		_log = log;
		_api = api;
		Names = names;

		Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
		_configSaver = new BackgroundSaver(
			() => _pluginInterface.SavePluginConfig(Config),
			exception => _log.Error(exception, "Couldn't save the settings"));

		// Only a dev build can point at another site. A dev build's settings carry over to the installed plugin, so
		// the installed one goes back to the real site, dropping the token, which was for the other one.
		IsDev = pluginInterface.IsDev;
		if (!IsDev && Config.ServerAddress != ServerAddress.Default)
		{
			_log.Information("Went back to {Server} from {Other}, which only dev builds can use", ServerAddress.Default, Config.ServerAddress);
			Config.ServerAddress = ServerAddress.Default;
			Config.Token = null;
			Config.Username = null;
			SaveConfig();
		}

		Log = new RunLog(store.LoadRuns());
		_runsSaver = new BackgroundSaver(
			() => store.SaveRuns(Log.Runs),
			exception => _log.Error(exception, "Couldn't save the runs"));
		Log.Changed += _runsSaver.Request;

		Uploader = new Uploader(Log, api, () => RunKeeper.ConnectionFor(Config));
		Keeper = new RunKeeper(Log, Uploader, Config);

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
		Log.Changed -= _runsSaver.Request;
		_runsSaver.Flush();
		_configSaver.Flush();
	}

	public void SaveConfig()
	{
		_configSaver.Request();
	}

	/// <summary>
	/// A run that just ended, on the framework thread: kept now, then uploaded if auto-upload's on, then the chat line
	/// </summary>
	public void RunEnded(Run run)
	{
		var duty = Names.Duty(run.GameContentId);
		var job = Names.Job(run.GameClassJobId);
		var outcome = Keeper.KeepAsync(run, _stopping.Token);

		Background(SayAsync, "upload the run that ended");

		async Task SayAsync()
		{
			var line = ChatLine.Format(run, duty, job, await outcome);
			if (Config.ChatLine && !_stopping.IsCancellationRequested)
			{
				await _framework.RunOnFrameworkThread(() => _chat.Print(line));
			}
		}
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
