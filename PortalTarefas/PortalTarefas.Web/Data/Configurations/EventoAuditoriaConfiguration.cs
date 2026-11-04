using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Data.Configurations;

public sealed class EventoAuditoriaConfiguration
    : IEntityTypeConfiguration<EventoAuditoria>
{
    public void Configure(
        EntityTypeBuilder<EventoAuditoria> builder)
    {
        builder.ToTable("EventosAuditoria");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Operacao)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(e => e.Descricao)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.DataHoraUtc)
            .IsRequired();

        builder.HasOne<Tarefa>()
            .WithMany()
            .HasForeignKey(e => e.TarefaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TarefaId);

        builder.HasIndex(e => e.DataHoraUtc);
    }
}
