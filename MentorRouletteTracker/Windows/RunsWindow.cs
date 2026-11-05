using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MentorRouletteTracker.Windows;

/// <summary>
/// The runs the plugin has recorded (<c>/mrt</c>)
/// </summary>
public sealed class RunsWindow : Window
{
	public RunsWindow() : base("Mentor Roulette Tracker##Runs")
	{
		Size = new Vector2(480, 300);
		SizeCondition = ImGuiCond.FirstUseEver;
	}

	public override void Draw()
	{
		ImGui.TextWrapped("No runs recorded yet.");
	}
}
