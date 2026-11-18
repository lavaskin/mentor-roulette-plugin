using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace MentorRouletteTracker.Windows;

/// <summary>
/// A popup asking to confirm a delete, with Delete and Cancel. A popup belongs to the window that opens it, so each
/// window has its own, and draws it every frame from its <c>Draw</c>, outside any row's id.
/// </summary>
/// <param name="title">The popup's title, with an <c>##id</c> unique in its window</param>
internal sealed class ConfirmDelete(string title)
{
	private bool _opening;

	public void Open()
	{
		_opening = true;
	}

	/// <param name="lines">What it says, read every frame so counts stay current. Null closes it: there's nothing
	/// left to delete.</param>
	/// <returns>Whether Delete was clicked</returns>
	public bool Draw(Func<IReadOnlyList<string>?> lines)
	{
		if (_opening)
		{
			ImGui.OpenPopup(title);
			_opening = false;
		}

		if (!ImGui.BeginPopupModal(title, ImGuiWindowFlags.AlwaysAutoResize))
		{
			return false;
		}

		var deleted = false;
		var text = lines();
		if (text is not null)
		{
			foreach (var line in text)
			{
				ImGui.TextUnformatted(line);
			}

			ImGui.Spacing();
			if (ImGui.Button("Delete"))
			{
				deleted = true;
				ImGui.CloseCurrentPopup();
			}

			ImGui.SameLine();
		}

		if (ImGui.Button("Cancel") || text is null)
		{
			ImGui.CloseCurrentPopup();
		}

		ImGui.EndPopup();
		return deleted;
	}
}
