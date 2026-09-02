using System.Text.Json.Nodes;

namespace BlazorApp1.Entities;

public class PythonScript
{
    public required string Name { get; init; }

    public string GetContent()
    {
        return File.ReadAllText($"./Scripts/{Name}/script.py");
    }

    public async Task SetContent(string content)
    {
        await File.WriteAllTextAsync($"./Scripts/{Name}/script.py", content);
    }

    public List<PythonPackage> GetRequirements()
    {
        if (!File.Exists($"./Scripts/{Name}/requirements.txt")) return [];
        var reqTxt =  File.ReadAllText($"./Scripts/{Name}/requirements.txt");
        return reqTxt.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(PythonPackage.FromString).ToList();
    }
    
    public void SetRequirements(List<PythonPackage> requirements)
    {
        if (requirements.Count == 0)
        {
            File.Delete($"./Scripts/{Name}/requirements.txt");
            return;
        }
        var reqTxt = string.Join("\n", requirements.Select(p => p.ToString()));
        File.WriteAllText($"./Scripts/{Name}/requirements.txt", reqTxt);
    }

    public JsonNode? GetParameterManifest()
    {
        try
        {
            var paramTxt = File.ReadAllText($"./Scripts/{Name}/params.json");
            return JsonNode.Parse(paramTxt);
        }
        catch
        {
            return null;
        }
    }

    public List<string> GetTags()
    {
        try
        {
            var scriptJson = File.ReadAllText($"./Scripts/{Name}/script.json");
            var tags = JsonNode.Parse(scriptJson)?["tags"]?.AsArray().Select(tag => tag!.ToString()).ToList();
            return tags ?? [];
        }
        catch
        {
            return [];
        }
    }

    public string? GetPolicy()
    {
        try
        {
            var scriptJson = File.ReadAllText($"./Scripts/{Name}/script.json");
            var policy = JsonNode.Parse(scriptJson)?["policy"]?.AsValue().ToString();
            return policy==""?null:policy;
        }
        catch
        {
            return null;
        }
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