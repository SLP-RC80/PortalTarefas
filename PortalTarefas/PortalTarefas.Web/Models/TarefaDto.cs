using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Models;

public sealed class TarefaDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "O título é obrigatório.")]
    [StringLength(120, ErrorMessage = "O título deve ter no máximo 120 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "A situação é obrigatória.")]
    public string Situacao { get; set; } = "Pendente";
}
