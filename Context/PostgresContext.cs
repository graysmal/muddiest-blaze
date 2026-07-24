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

    public virtual DbSet<Log> Logs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Log>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("log");

            entity.Property(e => e.ClientIp).HasColumnName("client_ip");
            entity.Property(e => e.ElapsedMs).HasColumnName("elapsed_ms");
            entity.Property(e => e.Exception).HasColumnName("exception");
            entity.Property(e => e.Level)
                .HasMaxLength(50)
                .HasColumnName("level");
            entity.Property(e => e.MachineName).HasColumnName("machine_name");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.MessageTemplate).HasColumnName("message_template");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.PreferredUsername).HasColumnName("preferred_username");
            entity.Property(e => e.Properties)
                .HasColumnType("jsonb")
                .HasColumnName("properties");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.RequestMethod).HasColumnName("request_method");
            entity.Property(e => e.RequestPath).HasColumnName("request_path");
            entity.Property(e => e.StatusCode).HasColumnName("status_code");
            entity.Property(e => e.Timestamp)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("timestamp");
            entity.Property(e => e.TraceId).HasColumnName("trace_id");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
