namespace BlazorApp1.Entities;

public class PythonRun
{
    public Guid Id { get; set; }
    public string ScriptName { get; set; }
    public string Status { get; set; }
    public string User { get; set; } // TODO create relation to a users table or somehow relate to entra, idk.
    public DateTime Started { get; set; }
    public DateTime? Ended { get; set; }
    public string? Params { get; set; }
    public bool HasOutput { get; set; }

    public bool IsOutputExpired()
    {
        var runPath = $"{Path.GetTempPath()}Scripts/run-{Id}";
        return !File.Exists(Path.Combine(runPath, $"run-{Id}-output.zip"));
    }
}
