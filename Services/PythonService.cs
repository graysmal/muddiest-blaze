using System.Diagnostics;
using BlazorApp1.Entities;

namespace BlazorApp1.Services;

public class PythonService
{
    public void Run(PythonScript script)
    {
        var pyFileDirPath = $"{Path.GetTempPath()}Scripts/run";
        if (!Directory.Exists(pyFileDirPath))
        {
            Directory.CreateDirectory(pyFileDirPath);
        }
        var pyFilePath = $"{pyFileDirPath}/{script.Name}.py";
        File.WriteAllText(pyFilePath, script.Content);
        var proc = new Process();
        proc.StartInfo.FileName = "/usr/bin/python";
        proc.StartInfo.Arguments = $"\"{pyFilePath}\"";
        proc.StartInfo.WorkingDirectory = pyFileDirPath;
        proc.Start();
        proc.WaitForExit();
    }
}