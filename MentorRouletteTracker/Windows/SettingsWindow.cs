using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Windows;

/// <summary>
/// The plugin's settings (<c>/mrt config</c>)
/// </summary>
public sealed class SettingsWindow : Window
{
	private const int TokenLength = 200;

	private readonly Tracker _tracker;
	private readonly Action _openRuns;
	private readonly ConfirmDelete _deleteUploaded = new("Delete uploaded runs?##DeleteUploaded");
	private readonly ConfirmDelete _deleteAll = new("Delete all runs?##DeleteAll");
	private string _token = string.Empty;
	private string _serverAddress;
	private string? _serverProblem;
	private bool _connecting;
	private string? _connectProblem;

	internal SettingsWindow(Tracker tracker, Action openRuns) : base("Mentor Roulette Tracker settings##Settings")
	{
		_tracker = tracker;
		_openRuns = openRuns;
		_serverAddress = tracker.Config.ServerAddress;
		Size = new Vector2(480, 420);
		SizeCondition = ImGuiCond.FirstUseEver;
	}

	public override void Draw()
	{
		DrawConnection();

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		DrawOptions();

		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		DrawRunsKept();

		// Only for trying the plugin against a dev copy of the site
		ImGui.Spacing();
		if (_tracker.IsDev && ImGui.CollapsingHeader("Advanced (dev builds only)"))
		{
			DrawServerAddress();
		}

		DrawDeleteConfirms();
	}

	private void DrawConnection()
	{
		var config = _tracker.Config;
		if (_tracker.IsConnected)
		{
			Ui.ConnectedAs(config.Username, config.ServerAddress);
			ImGui.SameLine();
			if (ImGui.SmallButton("Disconnect"))
			{
				_tracker.Disconnect();
				_connectProblem = null;
			}

			if (_tracker.Uploader.NeedsReconnect)
			{
				Ui.Wrapped(Ui.WarningColour, "The site stopped accepting this token: it was revoked, or the account was "
					+ "disabled. Make a new one on your Account page and connect again.");
				DrawTokenInput();
			}

			var waiting = _tracker.Log.NotUploaded.Count;
			if (waiting > 0)
			{
				ImGui.TextWrapped($"{Ui.Plural(waiting, "run isn't", "runs aren't")} uploaded yet.");
				ImGui.SameLine();
				if (Ui.Link("Upload them from your runs"))
				{
					_openRuns();
				}
			}

			return;
		}

		ImGui.TextWrapped("Connect the plugin to your account on Mentor Roulette Tracker to log your runs there. Make a "
			+ "token on your Account page, then paste it here.");
		Ui.WebLink("Open your Account page", ServerAddress.Link(config.ServerAddress, "account?section=plugin"));
		ImGui.Spacing();
		DrawTokenInput();
	}

	private void DrawTokenInput()
	{
		ImGui.SetNextItemWidth(260 * ImGuiHelpers.GlobalScale);
		ImGui.InputText("##Token", ref _token, TokenLength, ImGuiInputTextFlags.Password);
		ImGui.SameLine();
		var token = _token.Trim();
		if (Ui.Button(_connecting ? "Connecting…##Connect" : "Connect##Connect", !_connecting && token.Length > 0))
		{
			Connect(token);
		}

		if (_connectProblem is { } problem)
		{
			Ui.Wrapped(Ui.ErrorColour, problem);
		}
	}

	private void Connect(string token)
	{
		_connecting = true;
		_connectProblem = null;
		_tracker.ConnectAsync(token).ContinueWith(task =>
		{
			_connecting = false;
			if (!task.IsCompletedSuccessfully)
			{
				_connectProblem = "Couldn't connect.";
				return;
			}

			var result = task.Result;
			switch (result.Kind)
			{
				case ApiResultKind.Ok:
					_token = string.Empty;
					break;
				case ApiResultKind.Unauthorized:
					_connectProblem = "The site didn't accept that token. Check you copied all of it, or make a new one.";
					break;
				default:
					_connectProblem = result.Message;
					break;
			}
		}, TaskScheduler.Default);
	}

	private void DrawOptions()
	{
		var config = _tracker.Config;

		var autoUpload = config.AutoUpload;
		if (ImGui.Checkbox("Upload each run when it ends", ref autoUpload))
		{
			config.AutoUpload = autoUpload;
			_tracker.SaveConfig();
		}

		Ui.Tooltip("Auto-upload. Any older runs that aren't uploaded go first, so the site numbers them in order.");

		var chatLine = config.ChatLine;
		if (ImGui.Checkbox("Say in chat when a run ends", ref chatLine))
		{
			config.ChatLine = chatLine;
			_tracker.SaveConfig();
		}

		Ui.Tooltip("And when runs that were waiting are logged.");

		ImGui.Indent();
		ImGui.BeginDisabled(!config.ChatLine);
		var chatLinePace = config.ChatLinePace;
		if (ImGui.Checkbox("Say how its time compares with the usual", ref chatLinePace))
		{
			config.ChatLinePace = chatLinePace;
			_tracker.SaveConfig();
		}

		ImGui.EndDisabled();
		Ui.Tooltip("Compared with other players' recent runs of the duty.");
		ImGui.Unindent();
	}

	private void DrawRunsKept()
	{
		var (total, uploaded) = RunCounts();
		ImGui.TextUnformatted(RunsKept(total, uploaded));

		var idle = !_tracker.Uploader.IsUploading;
		if (Ui.Button("Delete uploaded runs", idle && uploaded > 0))
		{
			_deleteUploaded.Open();
		}

		ImGui.SameLine();
		if (Ui.Button("Delete all runs", idle && total > 0))
		{
			_deleteAll.Open();
		}
	}

	private void DrawDeleteConfirms()
	{
		if (_deleteUploaded.Draw(DeleteUploadedLines))
		{
			_tracker.DeleteUploaded();
		}

		if (_deleteAll.Draw(DeleteAllLines))
		{
			_tracker.DeleteAll();
		}
	}

	private (int Total, int Uploaded) RunCounts()
	{
		var runs = _tracker.Log.Runs;
		return (runs.Count, runs.Count(run => run.Status == RunStatus.Uploaded));
	}

	private static string RunsKept(int total, int uploaded)
	{
		if (total == 0)
		{
			return "No runs kept in the plugin.";
		}

		var which = (total, uploaded) switch
		{
			(1, 0) => "not uploaded",
			(1, _) => "uploaded",
			(_, 0) => "none of them uploaded",
			_ when uploaded == total => "all of them uploaded",
			_ => $"{uploaded:N0} of them uploaded",
		};
		return $"Runs kept in the plugin: {total:N0}, {which}.";
	}

	private string[]? DeleteUploadedLines()
	{
		var (_, uploaded) = RunCounts();
		if (uploaded == 0)
		{
			return null;
		}

		return [
			$"Delete {Ui.Plural(uploaded, "uploaded run", "uploaded runs")}?",
			uploaded == 1 ? "It stays on the site." : "They stay on the site.",
		];
	}

	private string[]? DeleteAllLines()
	{
		var (total, uploaded) = RunCounts();
		if (total == 0)
		{
			return null;
		}

		var notUploaded = total - uploaded;
		var question = total == 1 ? "Delete 1 run?" : $"Delete all {total:N0} runs?";
		var then = notUploaded switch
		{
			0 => "Runs already on the site stay there.",
			_ when notUploaded == total => total == 1
				? "It isn't uploaded, so it'll be gone for good."
				: "None of them are uploaded, so they'll be gone for good.",
			1 => "1 isn't uploaded, so it'll be gone for good. Runs already on the site stay there.",
			_ => $"{notUploaded:N0} aren't uploaded, so they'll be gone for good. Runs already on the site stay there.",
		};
		return [question, then];
	}

	private void DrawServerAddress()
	{
		ImGui.TextWrapped("The site the plugin uploads to.");

		var connected = _tracker.IsConnected;
		ImGui.BeginDisabled(connected);
		ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
		ImGui.InputText("##ServerAddress", ref _serverAddress, 200);
		ImGui.SameLine();
		if (ImGui.Button("Save##ServerAddress"))
		{
			SaveServerAddress(_serverAddress);
		}

		ImGui.SameLine();
		if (ImGui.Button("Reset##ServerAddress"))
		{
			SaveServerAddress(ServerAddress.Default);
		}

		ImGui.EndDisabled();

		if (connected)
		{
			ImGui.TextDisabled("Disconnect to change it: the token is for this site.");
		}
		else if (_serverProblem is { } problem)
		{
			Ui.Wrapped(Ui.ErrorColour, problem);
		}
	}

	private void SaveServerAddress(string text)
	{
		if (ServerAddress.Normalize(text) is not { } address)
		{
			_serverProblem = "That isn't a web address. It starts with https:// (or http://).";
			return;
		}

		_serverProblem = null;
		_serverAddress = address;
		_tracker.Config.ServerAddress = address;
		_tracker.SaveConfig();
	}

	public override void OnOpen()
	{
		_serverAddress = _tracker.Config.ServerAddress;
		_serverProblem = null;
	}
}
