using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker;

/// <summary>
/// Duty and job names from the game's data, in the game's language, for showing runs. The site gets the ids only.
/// </summary>
internal sealed class GameNames
{
	private readonly IDataManager _data;
	private readonly ConcurrentDictionary<uint, string> _duties = new();
	private readonly ConcurrentDictionary<uint, string> _jobs = new();

	public GameNames(IDataManager data)
	{
		_data = data;
	}

	public string Duty(uint contentFinderConditionId)
	{
		return _duties.GetOrAdd(contentFinderConditionId, id =>
			_data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(id)?.Name.ExtractText() is { Length: > 0 } name
				? Names.Capitalize(name)
				: $"Unknown duty ({id})");
	}

	public string Job(uint classJobId)
	{
		// Row 0 is "adventurer", which isn't a job
		return _jobs.GetOrAdd(classJobId, id =>
			id != 0 && _data.GetExcelSheet<ClassJob>().GetRowOrDefault(id)?.Name.ExtractText() is { Length: > 0 } name
				? Names.Capitalize(name)
				: "Unknown job");
	}
}
