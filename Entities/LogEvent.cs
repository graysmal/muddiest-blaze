using System.Text.Json.Serialization;
using BlazorApp1.Services;

namespace BlazorApp1.Entities;

// This class is not an EF Core related object, it is the schema from Loki.
public class LogEvent
{
    public DateTime EventDate { get; set; }
    public string? Level { get; set; }
    public string? Name { get; init; }
    
    [JsonPropertyName("preferred_username")]
    public string? PreferredUsername { get; init; }
    public string? MachineName { get; init; }
    public string? UserAgent { get; init; }
    public string? ClientIp { get; init; }
    public string? RequestMethod { get; init; }
    public string? RequestPath { get; init; }
    public string? HttpMethod { get; init; }
    public string? Uri { get; init; }
    public int? StatusCode { get; init; }
    public string? Message { get; init; }
    public LokiException? Exception { get; init; }
    public string? ExceptionMessage { get; set; }
}