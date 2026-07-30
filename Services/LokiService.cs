using Audit.Core;
using BlazorApp1.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CollectionNeverUpdated.Global

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
        await using (var auditScope = await AuditScope.CreateAsync("Loki:QueryRange", () => new { }, cancellationToken: token))
        {
            var queryString = options.ToQueryString(); 
            auditScope.SetCustomField("query", queryString);
            var url = $"/loki/api/v1/query_range{queryString}";
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
    public required string Query { get; init; }
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
    public int Limit { get; init; } = 100;
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
    public required LokiData Data { get; init; }
}

public class LokiData
{
    public required List<LokiResult> Result { get; set; }
}

public class LokiResult
{
    public required LokiStream Stream { get; set; }
    public required List<List<string>> Values { get; set; }   
}

public class LokiStream
{
    public required string Level { get; set; }
}

public class LokiException
{
    public required string Message { get; set; }
}

