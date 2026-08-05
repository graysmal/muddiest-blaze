using System.Diagnostics;
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

    public void Run(PythonScript script)
    {
        var pg = _postgresContextFactory.CreateDbContext();
        var guid = Guid.NewGuid();
        pg.PythonRuns.Add(new PythonRun
        {
            Id = guid,
            ScriptId = script.Id,
            Started =  DateTime.UtcNow,
            User = "test",
            Status = "Running"
        });
        pg.SaveChanges();
        var pyFileDirPath = $"{Path.GetTempPath()}Scripts/run-{guid}";
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
        var run = pg.PythonRuns.First(r => r.Id == guid);
        run.Status = "Completed";
        run.Ended = DateTime.UtcNow;
        pg.SaveChanges();
    }
}