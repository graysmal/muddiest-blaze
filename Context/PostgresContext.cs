using BlazorApp1.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp1.Context;

public class PostgresContext : DbContext
{
    public PostgresContext()
    {
    }

    public PostgresContext(DbContextOptions<PostgresContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditEvent> AuditEvents { get; set; }
    public virtual DbSet<PythonRun> PythonRuns { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_event");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.EventType).HasColumnName("event_type");
            entity.Property(e => e.EventDate).HasColumnName("event_date");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.PreferredUsername).HasColumnName("preferred_username");
            entity.Property(e => e.ClientIp).HasColumnName("client_ip");
            entity.Property(e => e.MachineName).HasColumnName("machine_name");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.Data).HasColumnType("jsonb").HasColumnName("data");
        });

        modelBuilder.Entity<PythonRun>(entity =>
        {
            entity.ToTable("python_run");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ScriptName).HasColumnName("script_name");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.User).HasColumnName("user");
            entity.Property(e => e.Started).HasColumnName("started");
            entity.Property(e => e.Ended).HasColumnName("ended");
            entity.Property(e => e.Params).HasColumnName("params");
            entity.Property(e => e.HasOutput).HasColumnName("has_output");
        });
    }
}
