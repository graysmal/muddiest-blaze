using System;
using System.Collections.Generic;

namespace BlazorApp1.Entities;

public partial class Log
{
    public DateTime? Timestamp { get; set; }

    public string? Level { get; set; }

    public string? Name { get; set; }

    public string? PreferredUsername { get; set; }

    public string? ClientIp { get; set; }

    public string? MachineName { get; set; }

    public string? UserAgent { get; set; }

    public string? RequestPath { get; set; }

    public string? RequestMethod { get; set; }

    public int? StatusCode { get; set; }

    public double? ElapsedMs { get; set; }

    public string? TraceId { get; set; }

    public string? RequestId { get; set; }

    public string? Message { get; set; }

    public string? MessageTemplate { get; set; }

    public string? Exception { get; set; }

    public string? Properties { get; set; }
}
