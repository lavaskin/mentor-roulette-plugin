using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using MentorRouletteTracker.Core.Api;

namespace MentorRouletteTracker.Windows;

internal static class Ui
{
	public static readonly Vector4 LinkColour = new(0.45f, 0.7f, 1f, 1f);
	public static readonly Vector4 WarningColour = new(1f, 0.75f, 0.3f, 1f);
	public static readonly Vector4 ErrorColour = new(1f, 0.45f, 0.45f, 1f);

	public static bool Link(string text)
	{
		ImGui.TextColored(LinkColour, text);
		if (ImGui.IsItemHovered())
		{
			ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}

		return ImGui.IsItemClicked();
	}

	public static void WebLink(string text, string url)
	{
		if (Link(text))
		{
			Util.OpenLink(url);
		}

		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(url);
		}
	}

	public static void ConnectedAs(string? username, string serverAddress)
	{
		ImGui.TextUnformatted("Connected as ");
		ImGui.SameLine(0, 0);
		WebLink(username ?? "", ServerAddress.Link(serverAddress, "account"));
	}

	public static void Wrapped(Vector4 colour, string text)
	{
		ImGui.PushStyleColor(ImGuiCol.Text, colour);
		ImGui.TextWrapped(text);
		ImGui.PopStyleColor();
	}

	public static bool Button(string label, bool enabled = true)
	{
		ImGui.BeginDisabled(!enabled);
		var clicked = ImGui.Button(label);
		ImGui.EndDisabled();
		return clicked;
	}

	public static string Plural(int count, string one, string many)
	{
		return count == 1 ? $"{count} {one}" : $"{count:N0} {many}";
	}

	public static void Tooltip(string text)
	{
		if (ImGui.IsItemHovered())
		{
			ImGui.SetTooltip(text);
		}
	}

	public static string Shorten(string text, int length)
	{
		return text.Length <= length ? text : text[..Math.Max(0, length - 1)].TrimEnd() + "…";
	}
}
