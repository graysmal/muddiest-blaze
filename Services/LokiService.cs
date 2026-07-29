using Audit.Core;
using BlazorApp1.Entities;
using System.Text.Json;

namespace BlazorApp1.Services;

public class LokiService
{
    private readonly HttpClient _httpClient;
    
    public LokiService(HttpClient httpClient)
    {
        _httpClient =  httpClient;
    }

    public async Task<List<LogEvent>> RunQueryRange(CancellationToken token)
    {
        
        IEnumerable<LogEvent> data = [];
        await using (await AuditScope.CreateAsync("Loki:Read", () => new { }, cancellationToken: token))
        {
            // TODO: either cache data and retrieve all logs (maybe 7 day max), or make method parameterized so that all logs can be paginated through.
            var response =
                await _httpClient.GetAsync("http://francis2:3100/loki/api/v1/query_range?query={app=%22web_app%22}&limit=5000",
                    token);
            var lokiResponse = await response.Content.ReadFromJsonAsync<LokiResponse>(token);
            foreach (var result in lokiResponse!.Data.Result)
            {
                foreach (var value in result.Values)
                {
                    var logEvent = JsonSerializer.Deserialize<LogEvent>(value[1])!;
                    var milliseconds = long.Parse(value[0]) / 1000000;
                    logEvent.EventDate = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime;
                    logEvent.Level = result.Stream.Level;
                    if (logEvent.Exception != null)
                    {
                        logEvent.ExceptionMessage = logEvent.Exception.Message;
                    }

                    data = data.Append(logEvent);
                }
            }
        }
        return [.. data];
    }
}