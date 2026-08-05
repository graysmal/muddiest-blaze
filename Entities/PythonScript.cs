namespace BlazorApp1.Entities;

public class PythonScript
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
}