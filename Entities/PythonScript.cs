namespace BlazorApp1.Entities;

public class PythonScript
{
    public string Name { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    public string GetContent()
    {
        return File.ReadAllText($"./Scripts/{Name}/script.py");
    }
    
    public List<PythonPackage> GetRequirements()
    {
        var reqTxt =  File.ReadAllText($"./Scripts/{Name}/requirements.txt");
        return reqTxt.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => PythonPackage.FromString(line)).ToList();
    }
}