using System.Net;
using System.Text;
using System.Text.Json;
using MentorRouletteTracker.Core.Api;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Tests;

public class MentorApiClientTests
{
	private static readonly ApiConnection Connection = new("https://mentor.example/", "mrt_secret");

	private readonly FakeHandler _handler = new();
	private readonly MentorApiClient _client;

	public MentorApiClientTests()
	{
		_client = new MentorApiClient(MentorApiClient.CreateHttpClient("1.2.3", _handler));
	}

	[Fact]
	public async Task LoggingARun_SendsWhatARunNeeds_AndNothingElse()
	{
		_handler.Respond(HttpStatusCode.Created, """{ "mentorRouletteLogId": 5123, "runNumber": 123, "dutyName": "The Vault", "durationDropped": false }""");
		var run = Runs.Make(hour: 3) with { Replacement = true, Wipes = 2, DurationSeconds = 1930, Status = RunStatus.Failed, FailedReason = "x" };

		var result = await _client.LogRunAsync(Connection, run, CancellationToken.None);

		Assert.Equal(ApiResult<LoggedRun>.Ok(new LoggedRun(5123, 123)), result);

		var request = _handler.Requests.Single();
		Assert.Equal(HttpMethod.Post, request.Method);
		Assert.Equal("https://mentor.example/api/plugin/runs", request.Uri);
		Assert.Equal("Bearer mrt_secret", request.Authorization);
		Assert.Equal("MentorRouletteTracker/1.2.3", request.UserAgent);

		using var body = JsonDocument.Parse(request.Body!);
		Assert.Equal(
			["pluginRunId", "gameContentId", "gameClassJobId", "completed", "replacement", "wipes", "durationSeconds", "datePlayed"],
			body.RootElement.EnumerateObject().Select(property => property.Name));
		Assert.Equal(run.Id, body.RootElement.GetProperty("pluginRunId").GetGuid());
		Assert.Equal(421, body.RootElement.GetProperty("gameContentId").GetInt32());
		Assert.Equal(19, body.RootElement.GetProperty("gameClassJobId").GetInt32());
		Assert.True(body.RootElement.GetProperty("completed").GetBoolean());
		Assert.True(body.RootElement.GetProperty("replacement").GetBoolean());
		Assert.Equal(2, body.RootElement.GetProperty("wipes").GetInt32());
		Assert.Equal(1930, body.RootElement.GetProperty("durationSeconds").GetInt32());
		Assert.Equal("2026-11-05T03:00:00Z", body.RootElement.GetProperty("datePlayed").GetString());
	}

	[Fact]
	public async Task AnAlreadyLoggedRun_ComesBackWithA200()
	{
		_handler.Respond(HttpStatusCode.OK, """{ "mentorRouletteLogId": 5123, "runNumber": 123 }""");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(new LoggedRun(5123, 123), result.Value);
	}

	[Fact]
	public async Task ARunsSiteId_CanBeALong_LikeTheApis()
	{
		_handler.Respond(HttpStatusCode.Created, """{ "mentorRouletteLogId": 3000000000, "runNumber": 123 }""");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(new LoggedRun(3_000_000_000, 123), result.Value);
	}

	[Fact]
	public async Task Me_GivesTheUsername()
	{
		_handler.Respond(HttpStatusCode.OK, """{ "username": "lavaskin" }""");

		var result = await _client.GetUsernameAsync(Connection, CancellationToken.None);

		Assert.Equal(ApiResult<string>.Ok("lavaskin"), result);
		var request = _handler.Requests.Single();
		Assert.Equal(HttpMethod.Get, request.Method);
		Assert.Equal("https://mentor.example/api/plugin/me", request.Uri);
		Assert.Equal("Bearer mrt_secret", request.Authorization);
	}

	[Fact]
	public async Task A400_IsRefused_WithTheSitesMessage()
	{
		_handler.Respond(HttpStatusCode.BadRequest, """{ "error": "That duty isn't in the Mentor Roulette." }""");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(ApiResultKind.Refused, result.Kind);
		Assert.Equal("That duty isn't in the Mentor Roulette.", result.Message);
	}

	[Fact]
	public async Task A400ValidationProblem_IsRefused_WithItsFirstError()
	{
		_handler.Respond(HttpStatusCode.BadRequest, """{ "title": "One or more validation errors occurred.", "errors": { "Wipes": ["Wipes must be 0 to 99."] } }""");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(ApiResultKind.Refused, result.Kind);
		Assert.Equal("Wipes must be 0 to 99.", result.Message);
	}

	[Fact]
	public async Task A400WithoutAMessage_IsStillRefused()
	{
		_handler.Respond(HttpStatusCode.BadRequest, "nope");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(new ApiResult<LoggedRun>(ApiResultKind.Refused, Message: "The site refused it."), result);
	}

	[Fact]
	public async Task A401_IsUnauthorized()
	{
		_handler.Respond(HttpStatusCode.Unauthorized, "");

		var result = await _client.GetUsernameAsync(Connection, CancellationToken.None);

		Assert.Equal(ApiResultKind.Unauthorized, result.Kind);
	}

	[Theory]
	[InlineData(HttpStatusCode.InternalServerError, "The site couldn't take it right now.")]
	[InlineData(HttpStatusCode.ServiceUnavailable, "The site couldn't take it right now.")]
	[InlineData(HttpStatusCode.TooManyRequests, "The site couldn't take it right now.")]
	[InlineData(HttpStatusCode.NotFound, "The site couldn't take it right now.")]
	[InlineData(HttpStatusCode.BadGateway, "Couldn't reach the site.")]
	[InlineData(HttpStatusCode.GatewayTimeout, "Couldn't reach the site.")]
	public async Task OtherFailures_AreUnavailable_ToTryAgainLater_InPlainWords(HttpStatusCode status, string message)
	{
		_handler.Respond(status, """{ "error": "Couldn't look that duty up on XIVAPI, so the run wasn't logged. Try again later." }""");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(new ApiResult<LoggedRun>(ApiResultKind.Unavailable, Message: message), result);
	}

	[Fact]
	public async Task A400WithOnlyAProblemTitle_GivesNoHttpWords()
	{
		_handler.Respond(HttpStatusCode.BadRequest, """{ "title": "Bad Request", "status": 400 }""");

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(new ApiResult<LoggedRun>(ApiResultKind.Refused, Message: "The site refused it."), result);
	}

	[Fact]
	public async Task NotReachingTheSite_IsUnavailable()
	{
		_handler.Throw(new HttpRequestException("Connection refused"));

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(new ApiResult<LoggedRun>(ApiResultKind.Unavailable, Message: "Couldn't reach the site."), result);
	}

	[Fact]
	public async Task ATimeout_IsUnavailable()
	{
		_handler.Throw(new TaskCanceledException("Timed out"));

		var result = await _client.LogRunAsync(Connection, Runs.Make(hour: 1), CancellationToken.None);

		Assert.Equal(ApiResultKind.Unavailable, result.Kind);
	}

	[Fact]
	public async Task AServerAddressThatIsntOne_IsUnavailable_WithoutARequest()
	{
		var result = await _client.GetUsernameAsync(Connection with { ServerAddress = "mentor dot lavask dot in" }, CancellationToken.None);

		Assert.Equal(ApiResultKind.Unavailable, result.Kind);
		Assert.Empty(_handler.Requests);
	}

	[Fact]
	public void Requests_HaveATimeout()
	{
		Assert.Equal(MentorApiClient.Timeout, MentorApiClient.CreateHttpClient("1.0.0").Timeout);
	}

	private sealed class FakeHandler : HttpMessageHandler
	{
		private HttpStatusCode _status = HttpStatusCode.OK;
		private string _body = "{}";
		private Exception? _exception;

		public List<SentRequest> Requests { get; } = [];

		public void Respond(HttpStatusCode status, string body)
		{
			_status = status;
			_body = body;
		}

		public void Throw(Exception exception)
		{
			_exception = exception;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Requests.Add(new SentRequest(
				request.Method,
				request.RequestUri!.ToString(),
				request.Headers.Authorization?.ToString(),
				request.Headers.UserAgent.ToString(),
				request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

			if (_exception is not null)
			{
				throw _exception;
			}

			return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
		}
	}

	private sealed record SentRequest(HttpMethod Method, string Uri, string? Authorization, string UserAgent, string? Body);
}
