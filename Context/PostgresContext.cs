using System;
using System.Collections.Generic;
using BlazorApp1.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp1.Context;

public partial class PostgresContext : DbContext
{
    public PostgresContext()
    {
    }

    public PostgresContext(DbContextOptions<PostgresContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditEvent> AuditEvents { get; set; }
    public virtual DbSet<PythonScript> PythonScripts { get; set; }
    public virtual DbSet<PythonRun> PythonRuns { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql();

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
        
        modelBuilder.Entity<PythonScript>(entity =>
        {
            entity.ToTable("python_script");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Created).HasColumnName("created");
            entity.Property(e => e.Modified).HasColumnName("modified");
        });

        modelBuilder.Entity<PythonRun>(entity =>
        {
            entity.ToTable("python_run");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ScriptId).HasColumnName("script_id");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.User).HasColumnName("user");
            entity.Property(e => e.Started).HasColumnName("started");
            entity.Property(e => e.Ended).HasColumnName("ended");
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
