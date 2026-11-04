using Microsoft.EntityFrameworkCore;
using PortalTarefas.Tests.Fixtures;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Tests.Services;

public class TarefaServiceTests
{
    [Fact]
    public async Task CriarAsync_DevePersistirTarefaNoBanco()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var service = new TarefaService(context);

        var tarefa = new Tarefa
        {
            Titulo = "Teste automatizado SQLite",
            Descricao = "Validar persistência do serviço.",
            Prioridade = 2
        };

        var criada = await service.CriarAsync(tarefa);

        Assert.True(criada.Id > 0);

        var recuperada = await context.Tarefas
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == criada.Id);

        Assert.NotNull(recuperada);

        Assert.Equal(
            "Teste automatizado SQLite",
            recuperada.Titulo);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarTarefasOrdenadasPorId()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var primeira = new Tarefa
        {
            Titulo = "Primeira tarefa",
            Prioridade = 1
        };

        var segunda = new Tarefa
        {
            Titulo = "Segunda tarefa",
            Prioridade = 2
        };

        context.Tarefas.AddRange(primeira, segunda);

        await context.SaveChangesAsync();

        var service = new TarefaService(context);

        var resultado = await service.ListarAsync();

        Assert.Equal(2, resultado.Count);

        Assert.Equal(
            new[] { primeira.Id, segunda.Id },
            resultado.Select(t => t.Id).ToArray());
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveRetornarTarefaExistente()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var tarefa = new Tarefa
        {
            Titulo = "Consulta por identificador",
            Prioridade = 2
        };

        context.Tarefas.Add(tarefa);

        await context.SaveChangesAsync();

        var service = new TarefaService(context);

        var resultado = await service.ObterPorIdAsync(tarefa.Id);

        Assert.NotNull(resultado);

        Assert.Equal(tarefa.Id, resultado.Id);

        Assert.Equal(
            "Consulta por identificador",
            resultado.Titulo);
    }

    [Fact]
    public async Task ObterPorIdAsync_DeveRetornarNuloQuandoNaoEncontrada()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var service = new TarefaService(context);

        var resultado = await service.ObterPorIdAsync(999);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task AtualizarAsync_DeveAtualizarTarefaERegistrarAuditoria()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var tarefa = new Tarefa
        {
            Titulo = "Tarefa original",
            Descricao = "Descrição original",
            Prioridade = 1
        };

        context.Tarefas.Add(tarefa);

        await context.SaveChangesAsync();

        var versaoOriginal = tarefa.Versao;

        var service = new TarefaService(context);

        var atualizacao = new Tarefa
        {
            Id = tarefa.Id,
            Titulo = "Tarefa atualizada",
            Descricao = "Descrição atualizada",
            Prioridade = 3,
            Prazo = DateTime.Today.AddDays(2),
            Versao = versaoOriginal
        };

        var resultado = await service.AtualizarAsync(atualizacao);

        Assert.True(resultado);

        context.ChangeTracker.Clear();

        var recuperada = await context.Tarefas
            .AsNoTracking()
            .SingleAsync(t => t.Id == tarefa.Id);

        Assert.Equal("Tarefa atualizada", recuperada.Titulo);
        Assert.Equal("Descrição atualizada", recuperada.Descricao);
        Assert.Equal(3, recuperada.Prioridade);

        Assert.NotEqual(versaoOriginal, recuperada.Versao);

        var evento = await context.EventosAuditoria
            .AsNoTracking()
            .SingleAsync(e => e.TarefaId == tarefa.Id);

Assert.True(evento.Id > 0);

Assert.Equal("ATUALIZACAO", evento.Operacao);

Assert.Contains("Tarefa atualizada", evento.Descricao);
    }

    [Fact]
    public async Task AtualizarAsync_DeveRetornarFalsoQuandoTarefaNaoExiste()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var service = new TarefaService(context);

        var tarefa = new Tarefa
        {
            Id = 999,
            Titulo = "Tarefa inexistente",
            Prioridade = 2,
            Versao = Guid.NewGuid()
        };

        var resultado = await service.AtualizarAsync(tarefa);

        Assert.False(resultado);

        Assert.Empty(context.EventosAuditoria);
    }

    [Fact]
    public async Task AtualizarAsync_DeveRejeitarVersaoDesatualizada()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        await using var context =
            database.CreateContext();

        var tarefa = new Tarefa
        {
            Titulo = "Teste de concorrência",
            Descricao = "Versão original",
            Prioridade = 2
        };

        context.Tarefas.Add(tarefa);

        await context.SaveChangesAsync();

        var versaoOriginal = tarefa.Versao;

        var service = new TarefaService(context);

        // Primeira atualização: deve ser aprovada.
        var primeiraAtualizacao = await service.AtualizarAsync(
            new Tarefa
            {
                Id = tarefa.Id,
                Titulo = "Alteração do usuário A",
                Descricao = "Primeira atualização",
                Prioridade = 2,
                Versao = versaoOriginal
            });

        Assert.True(primeiraAtualizacao);

        // Segunda atualização utiliza a versão antiga.
        var segundaAtualizacao = new Tarefa
        {
            Id = tarefa.Id,
            Titulo = "Alteração do usuário B",
            Descricao = "Tentativa com versão desatualizada",
            Prioridade = 3,
            Prazo = DateTime.Today.AddDays(2),
            Versao = versaoOriginal
        };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => service.AtualizarAsync(segundaAtualizacao));

        // Confirma que a primeira atualização foi preservada.
        await using var verificacao =
            database.CreateContext();

        var tarefaFinal = await verificacao.Tarefas
            .AsNoTracking()
            .SingleAsync(t => t.Id == tarefa.Id);

        Assert.Equal(
            "Alteração do usuário A",
            tarefaFinal.Titulo);

        // A segunda tentativa não pode gerar auditoria.
        var totalEventos = await verificacao.EventosAuditoria
            .CountAsync(e => e.TarefaId == tarefa.Id);

        Assert.Equal(1, totalEventos);
    }
}
