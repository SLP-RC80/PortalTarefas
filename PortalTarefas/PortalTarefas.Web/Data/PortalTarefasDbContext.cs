
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Data;

public sealed class PortalTarefasDbContext
    : IdentityDbContext<ApplicationUser>
{
    public PortalTarefasDbContext(
        DbContextOptions<PortalTarefasDbContext> options)
        : base(options)
    {
    }

    // Entidades existentes do domínio.
    public DbSet<Tarefa> Tarefas => Set<Tarefa>();

    public DbSet<EventoAuditoria> EventosAuditoria
        => Set<EventoAuditoria>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        // Configura primeiro as tabelas do Identity.
        base.OnModelCreating(modelBuilder);

        // Preserva as configurações das entidades existentes.
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
