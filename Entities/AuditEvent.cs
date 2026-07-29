namespace BlazorApp1.Entities;

public class AuditEvent
{
    public long Id { get; set; }
    public string? EventType { get; set; }
    public DateTime EventDate { get; set; }
    public string? Data { get; set; }
    public string? Name { get; set; }
    public string? PreferredUsername { get; set; }
    public string? ClientIp { get; set; }
    public string? MachineName { get; set; }
    public string? UserAgent { get; set; }
}