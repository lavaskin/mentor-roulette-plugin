using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace MentorRouletteTracker.Windows;

internal sealed class ConfirmDelete(string title)
{
	private bool _opening;

	public void Open()
	{
		_opening = true;
	}

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
