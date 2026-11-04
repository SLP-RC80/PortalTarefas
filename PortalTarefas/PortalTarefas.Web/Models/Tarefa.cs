
namespace PortalTarefas.Web.Models;

public sealed class Tarefa
{
    public int Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string? Descricao { get; set; }

    // 1 = Baixa | 2 = Média | 3 = Alta
    public int Prioridade { get; set; } = 2;

    public DateTime? Prazo { get; set; }

    // Token utilizado no controle de concorrência otimista.
    public Guid Versao { get; set; } = Guid.NewGuid();
}
