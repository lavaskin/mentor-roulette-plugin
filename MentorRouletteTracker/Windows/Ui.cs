using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace MentorRouletteTracker.Windows;

/// <summary>
/// Small pieces the windows share
/// </summary>
internal static class Ui
{
	public static readonly Vector4 LinkColour = new(0.45f, 0.7f, 1f, 1f);
	public static readonly Vector4 WarningColour = new(1f, 0.75f, 0.3f, 1f);
	public static readonly Vector4 ErrorColour = new(1f, 0.45f, 0.45f, 1f);
	public static readonly Vector4 GoodColour = new(0.5f, 0.85f, 0.5f, 1f);

	/// <summary>
	/// Text that works like a link
	/// </summary>
	/// <returns>Whether it was clicked</returns>
	public static bool Link(string text)
	{
		ImGui.TextColored(LinkColour, text);
		if (ImGui.IsItemHovered())
		{
			ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
		}

		return ImGui.IsItemClicked();
	}

	/// <summary>
	/// A link to a web page, opened in the browser, with its address as a tooltip
	/// </summary>
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
