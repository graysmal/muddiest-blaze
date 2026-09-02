namespace BlazorApp1.Entities;

public record PythonPackage
{
    public required string Name { get; init; }
    public required string Version { get; init; }

    public override string ToString()
    {
        return $"{Name}=={Version}";
    }
    
    public static PythonPackage FromString(string package)
    {
        var parts =  package.Split("==");
        return new PythonPackage
        {
            Name = parts[0],
            Version = parts[1]
        };
    }
}