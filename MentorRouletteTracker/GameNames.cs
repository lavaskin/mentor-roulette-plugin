using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using MentorRouletteTracker.Core.Text;

namespace MentorRouletteTracker;

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
		return _jobs.GetOrAdd(classJobId, id =>
			id != 0 && _data.GetExcelSheet<ClassJob>().GetRowOrDefault(id)?.Name.ExtractText() is { Length: > 0 } name
				? Names.Capitalize(name)
				: "Unknown job");
	}
}
