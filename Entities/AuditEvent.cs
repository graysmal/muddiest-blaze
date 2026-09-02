using System.ComponentModel.DataAnnotations;

namespace BlazorApp1.Entities;

public class AuditEvent
{
    public long Id { get; init; }
    [MaxLength(100)]
    public string? EventType { get; init; }
    public DateTime EventDate { get; init; }
    [MaxLength(3000)]
    public string? Data { get; init; }
    [MaxLength(100)]
    public string? Name { get; init; }
    [MaxLength(100)]
    public string? PreferredUsername { get; init; }
    [MaxLength(50)]
    public string? ClientIp { get; init; }
    [MaxLength(100)]
    public string? MachineName { get; init; }
    [MaxLength(100)]
    public string? UserAgent { get; init; }
}