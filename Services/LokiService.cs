using Audit.Core;
using BlazorApp1.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace BlazorApp1.Services;

public class LokiService
{
    private readonly HttpClient _httpClient;

    public LokiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // https://grafana.com/docs/loki/latest/reference/loki-http-api/#query-logs-within-a-range-of-time
    public async Task<List<LogEvent>> RunQueryRange(LokiQueryOptions options, CancellationToken token)
    {
        List<LogEvent> data = [];
        await using (await AuditScope.CreateAsync("Loki:Read", () => new { }, cancellationToken: token))
        {
            var url = $"/loki/api/v1/query_range{options.ToQueryString()}";
            var response = await _httpClient.GetAsync(url, token);
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
    
                    data.Add(logEvent);
                }
            }
        }
        return data;
    }
}


public class LokiQueryOptions
{
    public required string Query { get; set; }
    public DateTimeOffset? Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public int Limit { get; set; } = 100;
    public string Direction { get; set; } = "backward";

    public string ToQueryString()
    {
        var start = Start ?? DateTimeOffset.Now.AddDays(-7);
        var end = End ?? DateTimeOffset.Now;
        var qs = new Dictionary<string, string?>
        {
            { "query", Query },
            { "start", (start.ToUnixTimeMilliseconds() * 1000000).ToString() },
            { "end", (end.ToUnixTimeMilliseconds() * 1000000).ToString() },
            { "limit", Limit.ToString() },
            { "direction", Direction }
        };
        return QueryHelpers.AddQueryString("", qs);
    }
}

public class LokiResponse
{
    public LokiData Data { get; set; }
}

public class LokiData
{
    public List<LokiResult> Result { get; set; }
}

public class LokiResult
{
    public LokiStream Stream { get; set; }
    public List<List<string>> Values { get; set; }   
}

public class LokiStream
{
    public string Level { get; set; }
}

public class LokiException
{
    public string Message { get; set; }
}

