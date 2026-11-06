using MentorRouletteTracker.Core.Storage;

namespace MentorRouletteTracker.Core.Tests;

/// <summary>
/// <see cref="JsonFile"/> with a class shaped like the plugin's settings (a dev build's <c>dev/settings.json</c>):
/// settable properties with defaults, which the plugin's own settings class can't be here, since it's Dalamud's
/// </summary>
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
		};

		JsonFile.Write(_path, settings);
		var read = JsonFile.Read<Settings>(_path, _warnings.Add);

		Assert.NotNull(read);
		Assert.Equal(settings.ServerAddress, read.ServerAddress);
		Assert.Equal(settings.Token, read.Token);
		Assert.Equal(settings.Username, read.Username);
		Assert.Equal(settings.AutoUpload, read.AutoUpload);
		Assert.Equal(settings.ChatLine, read.ChatLine);
		Assert.Empty(_warnings);
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

	private sealed class Settings
	{
		public int Version { get; set; } = 1;
		public string ServerAddress { get; set; } = "https://mentor.lavask.in";
		public string? Token { get; set; }
		public string? Username { get; set; }
		public bool AutoUpload { get; set; } = true;
		public bool ChatLine { get; set; } = true;
	}
}
