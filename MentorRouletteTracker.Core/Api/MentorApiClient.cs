using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MentorRouletteTracker.Core.Models;

namespace MentorRouletteTracker.Core.Api;

public sealed class MentorApiClient : IMentorApi
{
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

	private const string CouldntReach = "Couldn't reach the site.";
	private const string CouldntTakeIt = "The site couldn't take it right now.";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly HttpClient _http;

	/// <param name="http">From <see cref="CreateHttpClient"/>, or a fake for tests</param>
	public MentorApiClient(HttpClient http)
	{
		_http = http;
	}

	/// <summary>
	/// An <see cref="HttpClient"/> with the plugin's <c>User-Agent</c> (<c>MentorRouletteTracker/1.0.0</c>) and a timeout
	/// </summary>
	public static HttpClient CreateHttpClient(string version, HttpMessageHandler? handler = null)
	{
		var http = handler is null ? new HttpClient() : new HttpClient(handler);
		http.Timeout = Timeout;
		http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MentorRouletteTracker", version));
		return http;
	}

	public async Task<ApiResult<string>> GetUsernameAsync(ApiConnection connection, CancellationToken cancellationToken)
	{
		var result = await SendAsync<MeResponse>(connection, HttpMethod.Get, "api/plugin/me", null, cancellationToken);
		return result.Kind == ApiResultKind.Ok
			? ApiResult<string>.Ok(result.Value!.Username)
			: new ApiResult<string>(result.Kind, Message: result.Message);
	}

	public async Task<ApiResult<LoggedRun>> LogRunAsync(ApiConnection connection, Run run, CancellationToken cancellationToken)
	{
		var body = new RunRequest(
			run.Id,
			run.GameContentId,
			run.GameClassJobId,
			run.Cleared,
			run.Replacement,
			run.Wipes,
			run.DurationSeconds,
			DateTime.SpecifyKind(run.DatePlayed, DateTimeKind.Utc));

		var result = await SendAsync<RunResponse>(connection, HttpMethod.Post, "api/plugin/runs", body, cancellationToken);
		return result.Kind == ApiResultKind.Ok
			? ApiResult<LoggedRun>.Ok(new LoggedRun(result.Value!.MentorRouletteLogId, result.Value.RunNumber))
			: new ApiResult<LoggedRun>(result.Kind, Message: result.Message);
	}

	private async Task<ApiResult<T>> SendAsync<T>(
		ApiConnection connection,
		HttpMethod method,
		string path,
		object? body,
		CancellationToken cancellationToken)
	{
		if (ServerAddress.Normalize(connection.ServerAddress) is not { } server)
		{
			return new ApiResult<T>(ApiResultKind.Unavailable, Message: "The server address isn't a web address.");
		}

		using var request = new HttpRequestMessage(method, ServerAddress.Link(server, path));
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
		if (body is not null)
		{
			request.Content = JsonContent.Create(body, options: JsonOptions);
		}

		HttpResponseMessage response;
		try
		{
			response = await _http.SendAsync(request, cancellationToken);
		}
		catch (HttpRequestException)
		{
			return new ApiResult<T>(ApiResultKind.Unavailable, Message: CouldntReach);
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return new ApiResult<T>(ApiResultKind.Unavailable, Message: "The site took too long to answer.");
		}

		using (response)
		{
			if (response.IsSuccessStatusCode)
			{
				try
				{
					var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
					return value is null
						? new ApiResult<T>(ApiResultKind.Unavailable, Message: CouldntTakeIt)
						: ApiResult<T>.Ok(value);
				}
				catch (JsonException)
				{
					return new ApiResult<T>(ApiResultKind.Unavailable, Message: CouldntTakeIt);
				}
			}

			// The messages are shown to players, so they never give status codes. A 502 or 504 comes from the proxy in
			// front of the site when the site itself is down.
			return response.StatusCode switch
			{
				HttpStatusCode.Unauthorized => new ApiResult<T>(ApiResultKind.Unauthorized, Message: "The site didn't accept the token."),
				HttpStatusCode.BadRequest => new ApiResult<T>(ApiResultKind.Refused, Message: await ReadErrorAsync(response, cancellationToken)),
				HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout => new ApiResult<T>(ApiResultKind.Unavailable, Message: CouldntReach),
				_ => new ApiResult<T>(ApiResultKind.Unavailable, Message: CouldntTakeIt),
			};
		}
	}

	/// <summary>
	/// The site's own message: <c>{ "error": "…" }</c>, or the first of a validation problem's errors
	/// </summary>
	private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		const string fallback = "The site refused it.";
		try
		{
			using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
			var root = json.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
			{
				return fallback;
			}

			if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
			{
				return error.GetString()!;
			}

			if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
			{
				var first = errors.EnumerateObject()
					.SelectMany(field => field.Value.ValueKind == JsonValueKind.Array ? field.Value.EnumerateArray() : [])
					.Where(message => message.ValueKind == JsonValueKind.String)
					.Select(message => message.GetString())
					.FirstOrDefault();
				if (!string.IsNullOrWhiteSpace(first))
				{
					return first;
				}
			}

			// Not a problem's title, which is HTTP's words ("Bad Request")
			return fallback;
		}
		catch (JsonException)
		{
			return fallback;
		}
	}

	private sealed record MeResponse(string Username);

	private sealed record RunRequest(
		Guid PluginRunId,
		uint GameContentId,
		uint GameClassJobId,
		bool Completed,
		bool Replacement,
		int Wipes,
		int DurationSeconds,
		DateTime DatePlayed);

	private sealed record RunResponse(long MentorRouletteLogId, int RunNumber);
}
