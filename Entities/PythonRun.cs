using System.ComponentModel.DataAnnotations;

namespace BlazorApp1.Entities;

public class PythonRun
{
    public Guid Id { get; init; }
    [MaxLength(100)]
    public required string ScriptName { get; init; }
    [MaxLength(100)]
    public required string Status { get; set; }
    [MaxLength(100)]
    public required string User { get; init; } // TODO create relation to a users table or somehow relate to entra, idk.
    public DateTime Started { get; init; }
    public DateTime? Ended { get; set; }
    [MaxLength(3000)]
    public string? Params { get; set; }
    public bool HasOutput { get; set; }

    public bool IsOutputExpired()
    {
        var runPath = $"{Path.GetTempPath()}Scripts/run-{Id}";
        return !File.Exists(Path.Combine(runPath, $"run-{Id}-output.zip"));
    }
}
