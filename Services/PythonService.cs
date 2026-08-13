using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Audit.Core;
using BlazorApp1.Context;
using BlazorApp1.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp1.Services;

public class PythonService
{
    private readonly IDbContextFactory<PostgresContext> _postgresContextFactory;

    public PythonService(IDbContextFactory<PostgresContext> postgresContextFactory)
    {
        _postgresContextFactory = postgresContextFactory;
    }
    
    public static List<PythonScript> GetPythonScripts()
    {
        var directories = Directory.GetDirectories("./Scripts");
        var scripts = directories.Select(d => new PythonScript
        {
            Name = Path.GetFileName(d)
        });
        return scripts.ToList();
    }

    public async Task RunAsync(PythonScript script, JsonNode? parameters, bool saveConsole, Action<Guid>? onGuidMade = null, 
        Action<string>? onOutputLine = null, Action<List<string>>? onOutputZip = null)
    {
        await using var auditScope = await AuditScope.CreateAsync("Python:Run", () => new { });
        // create run row in pg db table
        // TODO: set to setting up while making venv if necessary
        var pg = await _postgresContextFactory.CreateDbContextAsync();
        var guid = Guid.NewGuid();
        var run = new PythonRun
        {
            Id = guid,
            ScriptName = script.Name,
            Started = DateTime.UtcNow,
            User = auditScope.Event.CustomFields["preferred_username"].ToString()??"",
            Status = "Initializing",
            HasOutput = false
        };
        pg.PythonRuns.Add(run);
        await pg.SaveChangesAsync();
        onGuidMade?.Invoke(guid);
            
        // set up subprocess and output
        var proc = new Process();
        var consoleOutput = "";
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.OutputDataReceived += (_, e) =>
        {
            consoleOutput += e.Data + "\n";
            onOutputLine?.Invoke(e.Data??"");
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data.IsWhiteSpace()) return;
            consoleOutput += e.Data + "\n";
            onOutputLine?.Invoke(e.Data??"");
        };
            
        // create virtual environment if it doesn't exist.
        var scriptPath = $"{Directory.GetCurrentDirectory()}/Scripts/{script.Name}";
        await CreateScriptVenv(proc, scriptPath);
        // install/update requirements.txt
        if (File.Exists($"{scriptPath}/requirements.txt"))
        {
            await InstallRequirementsToVenv(proc, scriptPath);
        }
            
        // create temp file path for run
        var pyRunDirPath = $"{Path.GetTempPath()}Scripts/run-{guid}";
        Directory.CreateDirectory(pyRunDirPath);
        // copy script files to temp file path
        // TODO: copy recursively, excluding .venv/.
        foreach (var file in Directory.EnumerateFiles(scriptPath))
        {
            File.Copy(file, Path.Combine(pyRunDirPath, Path.GetFileName(file)), true);
        }
        if (parameters != null)
        {
            await File.WriteAllTextAsync($"{pyRunDirPath}/params.json", parameters.ToJsonString());
            run.Params = parameters.ToJsonString();
        }
        run.Status = "Running";
        await pg.SaveChangesAsync();
            
        // get list of file before running script to zip output later
        var preRunFiles = Directory.EnumerateFiles(pyRunDirPath).ToList();
        // run script
        proc.StartInfo.FileName = $"{scriptPath}/.venv/Scripts/python.exe";
        proc.StartInfo.Arguments = $"-u \"{pyRunDirPath}/script.py\"";
        proc.StartInfo.WorkingDirectory = pyRunDirPath;
        await RunWithOutput(proc);

        if (saveConsole)
        {
            await File.WriteAllTextAsync($"{pyRunDirPath}/console.log", consoleOutput);
        }
        // zip up all output files and return list of files
        var postRunFiles = Directory.EnumerateFiles(pyRunDirPath);
        postRunFiles = postRunFiles.Where(f => !preRunFiles.Contains(f)).ToList();
        var zipPath = $"{pyRunDirPath}/run-{guid}-output.zip";
        await using (var fs = new FileStream(zipPath, FileMode.Create))
        {
            await using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var file in postRunFiles)
                {
                    await archive.CreateEntryFromFileAsync(file, Path.GetFileName(file));
                }
            }
        }
        postRunFiles = postRunFiles.Select(f => Path.GetFileName(f));
        var postRunFilesList = postRunFiles.ToList();
        onOutputZip?.Invoke(postRunFilesList);
            
        // update run row
        run.Status = proc.ExitCode == 0?"Completed":"Failed";
        run.Ended = DateTime.UtcNow;
        run.HasOutput = postRunFilesList.Any();
        await pg.SaveChangesAsync();
    }

    private static async Task CreateScriptVenv(Process proc, string path)
    {
        proc.StartInfo.FileName = "uv";
        proc.StartInfo.Arguments = "venv --allow-existing";
        proc.StartInfo.WorkingDirectory = path;
        await RunWithOutput(proc);
    }

    private static async Task InstallRequirementsToVenv(Process proc, string path)
    {
        proc.StartInfo.FileName = "uv";
        proc.StartInfo.Arguments = "pip install -r requirements.txt";
        proc.StartInfo.WorkingDirectory = path;
        await RunWithOutput(proc);
    }

    private static async Task RunWithOutput(Process proc)
    {
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        proc.CancelErrorRead();
        proc.CancelOutputRead();
    }

    private async Task<bool> VerifyPythonInstall(Process proc)
    {
        proc.StartInfo.Arguments = "--version";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            proc.StartInfo.FileName = "python";
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            // TODO: check output of run to return true/false.
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            proc.StartInfo.FileName = "/usr/bin/python";
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            // TODO: check output of run to return true/false.
        }
        
        return true;
    }
    
    private async Task<bool> VerifyUVInstall(Process proc)
    {
        proc.StartInfo.Arguments = "--version";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            proc.StartInfo.FileName = "uv";
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            // TODO: check output of run to return true/false.
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            proc.StartInfo.FileName = "/usr/bin/uv";
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            // TODO: check output of run to return true/false.
        }

        return true;
    }

    private async Task InstallPython(Process proc)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            proc.StartInfo.FileName = "winget";
            proc.StartInfo.Arguments = "install -e --id Python.Python.3 --silent";
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.Start();
            await  proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            // TODO: verify this works.
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // TODO: install python on linux, may depend on distro, may require permissions
        }
    }

    private async Task InstallUV(Process proc)
    {
        proc.StartInfo.FileName = "python";
        proc.StartInfo.Arguments = "-m pip install uv";
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.Start();
        await  proc.WaitForExitAsync();
        proc.CancelErrorRead();
        proc.CancelOutputRead();
        // TODO: verify this works. likely that pip may need to be installed as well.
    }
}