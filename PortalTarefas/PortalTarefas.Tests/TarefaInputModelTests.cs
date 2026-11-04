using System.ComponentModel.DataAnnotations;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Tests;

public class TarefaInputModelTests
{
    private static List<ValidationResult> Validar(
        TarefaInputModel model)
    {
        var resultados = new List<ValidationResult>();

        var contexto = new ValidationContext(model);

        Validator.TryValidateObject(
            model,
            contexto,
            resultados,
            validateAllProperties: true);

        return resultados;
    }

    [Fact]
    public void ModeloValido_DeveSerAprovado()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Estudar ASP.NET Core MVC",
            Prioridade = 2
        };

        Assert.Empty(Validar(model));
    }

    [Fact]
    public void TituloVazio_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "",
            Prioridade = 2
        };

        var resultados = Validar(model);

        Assert.Contains(resultados,
            r => r.MemberNames.Contains(
                nameof(TarefaInputModel.Titulo)));
    }

    [Fact]
    public void TituloMuitoCurto_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "AB",
            Prioridade = 2
        };

        Assert.NotEmpty(Validar(model));
    }

    [Fact]
    public void DescricaoAcimaDe500Caracteres_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa válida",
            Descricao = new string('A', 501),
            Prioridade = 2
        };

        Assert.NotEmpty(Validar(model));
    }

    [Fact]
    public void PrioridadeForaDoIntervalo_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa válida",
            Prioridade = 4
        };

        Assert.NotEmpty(Validar(model));
    }

    [Fact]
    public void PrioridadeAltaSemPrazo_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa urgente",
            Prioridade = 3,
            Prazo = null
        };

        Assert.NotEmpty(Validar(model));
    }

    [Fact]
    public void PrioridadeAltaComPrazoPassado_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa urgente",
            Prioridade = 3,
            Prazo = DateTime.Today.AddDays(-1)
        };

        Assert.NotEmpty(Validar(model));
    }

    [Fact]
    public void PrioridadeAltaComPrazoSuperiorASeteDias_DeveGerarErro()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa urgente",
            Prioridade = 3,
            Prazo = DateTime.Today.AddDays(8)
        };

        Assert.NotEmpty(Validar(model));
    }

    [Fact]
    public void PrioridadeAltaComPrazoHoje_DeveSerAprovado()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa urgente",
            Prioridade = 3,
            Prazo = DateTime.Today
        };

        Assert.Empty(Validar(model));
    }

    [Fact]
    public void PrioridadeAltaComPrazoEmSeteDias_DeveSerAprovado()
    {
        var model = new TarefaInputModel
        {
            Titulo = "Tarefa urgente",
            Prioridade = 3,
            Prazo = DateTime.Today.AddDays(7)
        };

        Assert.Empty(Validar(model));
    }
}
