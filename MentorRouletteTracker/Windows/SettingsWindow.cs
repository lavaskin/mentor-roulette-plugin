using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Windows;

/// <summary>
/// The plugin's settings (<c>/mrt config</c>)
/// </summary>
public sealed class SettingsWindow : Window
{
	private const int TokenLength = 200;

	private readonly Tracker _tracker;
	private readonly Action _openRuns;
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

		// Only for trying the plugin against a dev copy of the site
		ImGui.Spacing();
		if (_tracker.IsDev && ImGui.CollapsingHeader("Advanced (dev builds only)"))
		{
			DrawServerAddress();
		}
	}

	private void DrawConnection()
	{
		var config = _tracker.Config;
		if (_tracker.IsConnected)
		{
			ImGui.TextUnformatted($"Connected as {config.Username}.");
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

		ImGui.Spacing();
		ImGui.TextWrapped("With auto-upload on, don't log your Mentor Roulette runs on the site as well: each would count twice.");
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
