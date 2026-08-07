using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Audit.Core;
using BlazorApp1.Context;
using BlazorApp1.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.FSharp.Core;

namespace BlazorApp1.Services;

public class PythonService
{
    private readonly IDbContextFactory<PostgresContext> _postgresContextFactory;

    public PythonService(IDbContextFactory<PostgresContext> postgresContextFactory)
    {
        _postgresContextFactory = postgresContextFactory;
    }
    
    public List<PythonScript> GetPythonScripts()
    {
        var directories = Directory.GetDirectories("./Scripts");
        var scripts = directories.Select(d => new PythonScript
        {
            Name = Path.GetFileName(d)
        });
        return scripts.ToList();
    }

    public async Task RunAsync(PythonScript script, JsonNode? _parameters, Action<Guid>? onGuidMade = null, 
        Action<string>? onOutputLine = null, Action<List<string>> onOutputZip = null)
    {
        await using (var auditScope = await AuditScope.CreateAsync("Python:Run", () => new { }))
        {
            // create run row in pg db table
            // TODO: set to setting up while making venv if necessary
            var pg = await _postgresContextFactory.CreateDbContextAsync();
            var guid = Guid.NewGuid();
            onGuidMade?.Invoke(guid);
            pg.PythonRuns.Add(new PythonRun
            {
                Id = guid,
                ScriptName =  script.Name,
                Started =  DateTime.UtcNow,
                User = auditScope.Event.CustomFields["preferred_username"].ToString(),
                Status = "Running"
            });
            await pg.SaveChangesAsync();
            
            // set up subprocess and output
            var proc = new Process();
            proc.StartInfo.FileName = "/usr/bin/uv";
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.OutputDataReceived += (s, e) =>
            {
                onOutputLine?.Invoke(e.Data);
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                onOutputLine?.Invoke(e.Data);
            };
            
            // create virtual environment if it doesn't exist.
            var scriptPath = $"{Directory.GetCurrentDirectory()}/Scripts/{script.Name}";
            
            proc.StartInfo.Arguments = "venv --allow-existing";
            proc.StartInfo.WorkingDirectory = scriptPath;
            proc.Start();
            
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            
            // install/update requirements.txt
            proc.StartInfo.Arguments = "pip install -r requirements.txt";
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            
            // create temp file path for run
            var pyRunDirPath = $"{Path.GetTempPath()}Scripts/run-{guid}";
            Directory.CreateDirectory(pyRunDirPath);
            
            // copy script files to temp file path
            // TODO: copy recursively, excluding .venv/.
            foreach (var file in Directory.EnumerateFiles(scriptPath))
            {
                File.Copy(file, Path.Combine(pyRunDirPath, Path.GetFileName(file)), true);
            }
            File.WriteAllText($"{pyRunDirPath}/params.json", _parameters.ToJsonString());
            var pyFilePath = $"{pyRunDirPath}/script.py";
            // overwrite params
            
            
            // get list of file before running script to zip output later
            var preRunFiles = Directory.EnumerateFiles(pyRunDirPath).ToList();
            
            // run script
            proc.StartInfo.FileName = $"{scriptPath}/.venv/bin/python";
            proc.StartInfo.Arguments = $"\"{pyFilePath}\"";
            proc.StartInfo.WorkingDirectory = pyRunDirPath;
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            proc.CancelErrorRead();
            proc.CancelOutputRead();
            
            // update run row
            var run = pg.PythonRuns.First(r => r.Id == guid);
            run.Status = "Completed";
            run.Ended = DateTime.UtcNow;
            await pg.SaveChangesAsync();

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
            onOutputZip.Invoke(postRunFiles.ToList());
        }
    }
}