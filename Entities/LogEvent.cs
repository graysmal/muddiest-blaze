using System.Text.Json.Serialization;

namespace BlazorApp1.Entities;

// This class is not an EF Core related object, it is the schema from Loki.
public class LogEvent
{
    public DateTime EventDate { get; set; }
    public string? Level { get; set; }
    public string? Name { get; set; }
    
    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; set; }
    public string? MachineName { get; set; }
    public string? UserAgent { get; set; }
    public string? ClientIp { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public string? Uri { get; set; }
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public LokiException? Exception { get; set; }
    public string? ExceptionMessage { get; set; }
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