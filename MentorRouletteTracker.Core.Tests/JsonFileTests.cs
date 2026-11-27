using MentorRouletteTracker.Core.Models;
using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Tests;

public sealed class JsonFileTests : IDisposable
{
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "mrt-tests-" + Guid.NewGuid());
	private readonly string _path;
	private readonly List<string> _warnings = [];

	public JsonFileTests()
	{
		Directory.CreateDirectory(_directory);
		_path = Path.Combine(_directory, "settings.json");
	}

	public void Dispose()
	{
		Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void Settings_ReadBackAsWritten()
	{
		var settings = new Settings
		{
			ServerAddress = "http://localhost:5000",
			Token = "mrt_abc",
			Username = "Lava",
			AutoUpload = false,
			ChatLine = false,
			ChatLinePace = false,
		};

		JsonFile.Write(_path, settings);
		var read = JsonFile.Read<Settings>(_path, _warnings.Add);

		Assert.Equivalent(settings, read, strict: true);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void SettingsSavedByADevBuild_StillRead()
	{
		File.WriteAllText(_path, """
			{
			  "version": 1,
			  "serverAddress": "http://localhost:4200",
			  "token": "mrt_abc",
			  "username": "Lava",
			  "autoUpload": false,
			  "chatLine": false,
			  "chatLinePace": false
			}
			""");

		var read = JsonFile.Read<Settings>(_path, _warnings.Add);

		Assert.Equivalent(new Settings
		{
			ServerAddress = "http://localhost:4200",
			Token = "mrt_abc",
			Username = "Lava",
			AutoUpload = false,
			ChatLine = false,
			ChatLinePace = false,
		}, read, strict: true);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void SettingsSavedBeforeThePaceSetting_ReadWithItOn()
	{
		File.WriteAllText(_path, """
			{
			  "version": 1,
			  "serverAddress": "https://mentorroulette.com",
			  "username": "Lava",
			  "autoUpload": true,
			  "chatLine": true
			}
			""");

		Assert.True(JsonFile.Read<Settings>(_path, _warnings.Add)!.ChatLinePace);
	}

	[Fact]
	public void NoFile_ReadsAsNothing()
	{
		Assert.Null(JsonFile.Read<Settings>(_path, _warnings.Add));
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AFileThatCantBeRead_IsSetAside_NotOverwritten()
	{
		File.WriteAllText(_path, "{ not json");

		Assert.Null(JsonFile.Read<Settings>(_path, _warnings.Add));

		Assert.False(File.Exists(_path));
		var setAside = Assert.Single(Directory.GetFiles(_directory, "settings.json.unreadable-*"));
		Assert.Equal("{ not json", File.ReadAllText(setAside));
		Assert.Single(_warnings);
	}

	[Fact]
	public void Writing_ReplacesTheFile_AndLeavesNoTemporaryFile()
	{
		JsonFile.Write(_path, new Settings { Token = "mrt_old" });
		JsonFile.Write(_path, new Settings { Token = null });

		Assert.Null(JsonFile.Read<Settings>(_path, _warnings.Add)!.Token);
		Assert.Equal(["settings.json"], Directory.GetFiles(_directory).Select(Path.GetFileName));
	}
}
