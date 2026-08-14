using System.Text.Json.Nodes;

namespace BlazorApp1.Entities;

public class PythonScript
{
    public string Name { get; set; }

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
            .Select(line => PythonPackage.FromString(line)).ToList();
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

    public string? GetReadmeContent()
    {
        var path = $"./Scripts/{Name}/README.md";
        return !File.Exists(path) ? null : File.ReadAllText(path);
    }

    public async Task SetReadmeContent(string content)
    {
        await File.WriteAllTextAsync($"./Scripts/{Name}/README.md", content);
    }

    public void DeleteReadme()
    {
        File.Delete($"./Scripts/{Name}/README.md");
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