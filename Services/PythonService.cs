using System.Diagnostics;
using System.IO.Compression;
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

    public async Task RunAsync(PythonScript script, Action<Guid>? onGuidMade = null, Action<string>? onOutputLine = null, Action<List<string>> onOutputZip = null)
    {
        // create run row in table
        var pg = await _postgresContextFactory.CreateDbContextAsync();
        var guid = Guid.NewGuid();
        onGuidMade?.Invoke(guid);
        pg.PythonRuns.Add(new PythonRun
        {
            Id = guid,
            ScriptId = script.Id,
            Started =  DateTime.UtcNow,
            User = "test",
            Status = "Running"
        });
        await pg.SaveChangesAsync();
        
        // create temp file path and create py script
        var pyFileDirPath = $"{Path.GetTempPath()}Scripts/run-{guid}";
        if (!Directory.Exists(pyFileDirPath))
        {
            Directory.CreateDirectory(pyFileDirPath);
        }
        var pyFilePath = $"{pyFileDirPath}/{script.Name}.py";
        await File.WriteAllTextAsync(pyFilePath, script.Content);
        var preRunFiles = Directory.EnumerateFiles(pyFileDirPath).ToList();
        
        // set up process and listeners
        var proc = new Process();
        proc.StartInfo.FileName = "/usr/bin/python";
        proc.StartInfo.Arguments = $"\"{pyFilePath}\"";
        proc.StartInfo.WorkingDirectory = pyFileDirPath;
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
        
        // run script
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        
        // update run row
        var run = pg.PythonRuns.First(r => r.Id == guid);
        run.Status = "Completed";
        run.Ended = DateTime.UtcNow;
        await pg.SaveChangesAsync();

        // zip up all output files and return list of files
        var postRunFiles = Directory.EnumerateFiles(pyFileDirPath);
        postRunFiles = postRunFiles.Where(f => !preRunFiles.Contains(f)).ToList();
        var zipPath = $"{pyFileDirPath}/run-{guid}-output.zip";
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