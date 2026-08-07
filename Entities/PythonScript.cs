using System.Text.Json.Nodes;

namespace BlazorApp1.Entities;

public class PythonScript
{
    public string Name { get; set; }

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

    public JsonNode? GetParameterManifest()
    {
        var paramTxt = File.ReadAllText($"./Scripts/{Name}/params.json");
        return JsonNode.Parse(paramTxt);
    }

    public DateTime GetDateCreated()
    {
        return File.GetCreationTimeUtc($"./Scripts/{Name}").ToLocalTime();
    }
    
    public DateTime GetDateModified()
    {
        return File.GetLastWriteTimeUtc($"./Scripts/{Name}").ToLocalTime();
    }
}