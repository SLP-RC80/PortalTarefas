namespace PortalTarefas.Web.Models;

public sealed class EventoAuditoria
{
    public int Id { get; set; }

    public int TarefaId { get; set; }

    public string Operacao { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public DateTimeOffset DataHoraUtc { get; set; }
}
