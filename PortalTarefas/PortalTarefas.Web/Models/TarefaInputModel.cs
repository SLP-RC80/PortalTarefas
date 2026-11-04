
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PortalTarefas.Web.Models;

public sealed class TarefaInputModel : IValidatableObject
{
    [Required(ErrorMessage = "O título é obrigatório.")]
    [StringLength(
        120,
        MinimumLength = 3,
        ErrorMessage = "O título deve possuir entre 3 e 120 caracteres."
    )]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(
        500,
        ErrorMessage = "A descrição deve ter no máximo 500 caracteres."
    )]
    public string? Descricao { get; set; }

    [Range(
        1,
        3,
        ErrorMessage = "Selecione uma prioridade entre 1 e 3."
    )]
    public int Prioridade { get; set; } = 2;

    [DataType(DataType.Date)]
    public DateTime? Prazo { get; set; }

// Versão original utilizada no controle de concorrência.
public Guid Versao { get; set; }

    // Validação personalizada entre prioridade e prazo.
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Prioridade == 3)
        {
            if (!Prazo.HasValue)
            {
                yield return new ValidationResult(
                    "Tarefas de prioridade alta devem possuir um prazo.",
                    new[] { nameof(Prazo) }
                );
            }
            else
            {
                DateTime hoje = DateTime.Today;
                DateTime prazo = Prazo.Value.Date;

                if (prazo < hoje)
                {
                    yield return new ValidationResult(
                        "O prazo não pode estar no passado.",
                        new[] { nameof(Prazo) }
                    );
                }
                else if (prazo > hoje.AddDays(7))
                {
                    yield return new ValidationResult(
                        "Tarefas de prioridade alta devem ter prazo dentro dos próximos 7 dias.",
                        new[] { nameof(Prazo) }
                    );
                }
            }
        }
    }
}
