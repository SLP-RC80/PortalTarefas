
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Data.Configurations;

public sealed class TarefaConfiguration
    : IEntityTypeConfiguration<Tarefa>
{
    public void Configure(EntityTypeBuilder<Tarefa> builder)
    {
        builder.ToTable("Tarefas");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        builder.Property(t => t.Titulo)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(t => t.Descricao)
            .HasMaxLength(500);

        builder.Property(t => t.Prioridade)
            .IsRequired();

        builder.Property(t => t.Prazo)
            .HasColumnType("timestamp without time zone")
            .IsRequired(false);

        // Configuração do token de concorrência.
        builder.Property(t => t.Versao)
            .IsRequired()
            .IsConcurrencyToken()
            .HasDefaultValueSql("gen_random_uuid()");
    }
}
