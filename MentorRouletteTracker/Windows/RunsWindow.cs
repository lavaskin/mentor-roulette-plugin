using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker.Windows;

public sealed class RunsWindow : Window
{
	private const string NumberingNote = "Runs are numbered in the order they reach the site. These will come after any "
		+ "runs you've logged since, but keep the dates they were played.";

	private readonly Tracker _tracker;
	private readonly Action _openSettings;
	private readonly ConfirmDelete _deleteRun = new("Delete run?##DeleteRun");
	private Run? _deleting;

	internal RunsWindow(Tracker tracker, Action openSettings) : base("Mentor Roulette Tracker##Runs")
	{
		_tracker = tracker;
		_openSettings = openSettings;
		Size = new Vector2(860, 420);
		SizeCondition = ImGuiCond.FirstUseEver;
	}

	public override void Draw()
	{
		DrawConnection();
		DrawUpload();
		ImGui.Spacing();
		DrawRuns();
		DrawDeleteConfirm();
	}

	private void DrawConnection()
	{
		var config = _tracker.Config;
		if (!_tracker.IsConnected)
		{
			ImGui.TextUnformatted("Not connected");
		}
		else if (_tracker.Uploader.NeedsReconnect)
		{
			ImGui.TextColored(Ui.WarningColour, "The site stopped accepting the plugin's token. Reconnect to upload.");
		}
		else
		{
			Ui.ConnectedAs(config.Username, config.ServerAddress);
		}

		const string settings = "Settings";
		ImGui.SameLine();
		var gap = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(settings).X;
		ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, gap));
		if (Ui.Link(settings))
		{
			_openSettings();
		}
	}

	private void DrawUpload()
	{
		var waiting = _tracker.Log.NotUploaded.Count;
		if (waiting == 0)
		{
			return;
		}

		ImGui.Spacing();
		var uploading = _tracker.Uploader.IsUploading;
		var label = uploading ? "Uploading…" : $"Upload {Ui.Plural(waiting, "run", "runs")}";
		if (Ui.Button($"{label}##Upload", _tracker.IsConnected && !uploading))
		{
			_tracker.UploadAll();
		}

		if (!_tracker.IsConnected)
		{
			ImGui.SameLine();
			ImGui.TextDisabled("Connect in settings to upload them.");
		}

		if (_tracker.Uploader.Problem is { } problem && !uploading)
		{
			var then = _tracker.Keeper.AutoUploading ? " It'll try again after your next run, or upload them now." : "";
			Ui.Wrapped(Ui.WarningColour, $"Couldn't upload: {problem}{then}");
		}

		ImGui.TextWrapped(NumberingNote);
	}

	private void DrawRuns()
	{
		var runs = _tracker.Log.Runs;
		if (runs.Count == 0)
		{
			ImGui.TextUnformatted("No runs recorded");
			return;
		}

		const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY
			| ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit;
		if (!ImGui.BeginTable("Runs", 10, flags))
		{
			return;
		}

		ImGui.TableSetupScrollFreeze(0, 1);
		ImGui.TableSetupColumn("Date");
		ImGui.TableSetupColumn("Duty", ImGuiTableColumnFlags.WidthStretch);
		ImGui.TableSetupColumn("Job");
		ImGui.TableSetupColumn("Cleared");
		ImGui.TableSetupColumn("Replacement");
		ImGui.TableSetupColumn("Wipes");
		ImGui.TableSetupColumn("Deaths");
		ImGui.TableSetupColumn("Time");
		ImGui.TableSetupColumn("Status");
		ImGui.TableSetupColumn("##Actions");
		ImGui.TableHeadersRow();

		var clipper = ImGui.ImGuiListClipper();
		clipper.Begin(runs.Count);
		while (clipper.Step())
		{
			for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
			{
				DrawRun(runs[i]);
			}
		}

		clipper.End();
		clipper.Destroy();
		ImGui.EndTable();
	}

	private void DrawRun(Run run)
	{
		ImGui.PushID(run.Id.ToString());
		ImGui.TableNextRow();

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(DateText.Format(run.DatePlayed));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(_tracker.Names.Duty(run.GameContentId));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(_tracker.Names.Job(run.GameClassJobId));
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(run.Cleared ? "Yes" : "No");
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(run.Replacement ? "Yes" : "No");
		ImGui.TableNextColumn();
		ImGui.TextUnformatted(run.Wipes.ToString("N0"));
		ImGui.TableNextColumn();
		if (run.Deaths is { } deaths)
		{
			ImGui.TextUnformatted(deaths.ToString("N0"));
		}

		ImGui.TableNextColumn();
		ImGui.TextUnformatted(Duration.Format(run.DurationSeconds));

		ImGui.TableNextColumn();
		DrawStatus(run);

		ImGui.TableNextColumn();
		if (run.Status == RunStatus.Failed)
		{
			if (Ui.Button("Retry", !_tracker.Uploader.IsUploading && _tracker.IsConnected))
			{
				_tracker.Retry(run.Id);
			}

			ImGui.SameLine();
		}

		if (run.Status != RunStatus.Uploaded && Ui.Button("Delete", !_tracker.Uploader.IsUploading))
		{
			_deleting = run;
			_deleteRun.Open();
		}

		ImGui.PopID();
	}

	private void DrawStatus(Run run)
	{
		switch (run.Status)
		{
			case RunStatus.Uploaded:
				Ui.WebLink(
					$"Uploaded #{run.RunNumber}",
					ServerAddress.Link(_tracker.Config.ServerAddress, $"roulettes?run={run.SiteRunId}"));
				break;

			case RunStatus.Failed:
				var reason = run.FailedReason ?? "The site refused it.";
				ImGui.TextColored(Ui.ErrorColour, Ui.Shorten($"Failed: {reason}", 48));
				Ui.Tooltip(reason);
				break;

			default:
				ImGui.TextUnformatted("Not uploaded");
				break;
		}
	}

	private void DrawDeleteConfirm()
	{
		var deleting = _deleting;
		if (_deleteRun.Draw(() => deleting is null
			? null
			: [
				$"Delete {_tracker.Names.Duty(deleting.GameContentId)} on {DateText.Format(deleting.DatePlayed)}?",
				"It hasn't been uploaded, so it'll be gone for good.",
			]))
		{
			_tracker.Delete(deleting!.Id);
			_deleting = null;
		}
	}
}
