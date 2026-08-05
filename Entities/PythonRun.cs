namespace BlazorApp1.Entities;

public class PythonRun
{
    public Guid Id { get; set; }
    public long ScriptId { get; set; }
    public string Status { get; set; }
    public string User { get; set; } // TODO create relation to a users table or somehow relate to entra, idk.
    public DateTime Started { get; set; }
    public DateTime? Ended { get; set; }
}