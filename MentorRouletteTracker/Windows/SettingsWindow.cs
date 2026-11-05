using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MentorRouletteTracker.Windows;

/// <summary>
/// The plugin's settings (<c>/mrt config</c>)
/// </summary>
public sealed class SettingsWindow : Window
{
	public SettingsWindow() : base("Mentor Roulette Tracker settings##Settings")
	{
		Size = new Vector2(400, 200);
		SizeCondition = ImGuiCond.FirstUseEver;
	}

	public override void Draw()
	{
		ImGui.TextWrapped("Nothing to set yet.");
	}
}
