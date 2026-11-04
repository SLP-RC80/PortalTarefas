using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Data;

public sealed class PortalTarefasDbContext : DbContext
{
    public PortalTarefasDbContext(
        DbContextOptions<PortalTarefasDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tarefa> Tarefas => Set<Tarefa>();

    public DbSet<EventoAuditoria> EventosAuditoria
        => Set<EventoAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PortalTarefasDbContext).Assembly);

        // Ajustes exclusivos para o banco SQLite de testes.
        if (Database.ProviderName ==
            "Microsoft.EntityFrameworkCore.Sqlite")
        {
            modelBuilder.Entity<Tarefa>()
                .Property(t => t.Versao)
                .HasDefaultValueSql((string?)null)
                .ValueGeneratedNever();

            modelBuilder.Entity<Tarefa>()
                .Property(t => t.Prazo)
                .HasColumnType("TEXT");
        }
    }
}