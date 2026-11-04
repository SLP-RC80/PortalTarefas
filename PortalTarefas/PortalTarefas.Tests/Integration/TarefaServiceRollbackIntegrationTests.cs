using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortalTarefas.Tests.Fixtures;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.Models;
using PortalTarefas.Web.Services;

namespace PortalTarefas.Tests.Integration;

public sealed class TarefaServiceRollbackIntegrationTests
    : IClassFixture<PortalTarefasApplicationFactory>
{
    private readonly PortalTarefasApplicationFactory _factory;

    public TarefaServiceRollbackIntegrationTests(
        PortalTarefasApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AtualizarAsync_ComVersaoDesatualizada_DeveExecutarRollback()
    {
        // Inicializa a aplicação de testes.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Tarefas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        int tarefaId;
        Guid versaoInicial;

        const string tituloUsuarioB =
            "Atualizada pelo usuario B";

        const string tituloUsuarioA =
            "Tentativa invalida do usuario A";

        // ==========================================
        // ETAPA 1 - CRIAR TAREFA
        // ==========================================

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<ITarefaService>();

            var criada = await service.CriarAsync(
                new Tarefa
                {
                    Titulo = "Tarefa para teste de rollback",
                    Descricao = "Registro inicial",
                    Prioridade = 2
                });

            tarefaId = criada.Id;
            versaoInicial = criada.Versao;
        }

        // ==========================================
        // ETAPA 2 - USUARIO B ATUALIZA COM SUCESSO
        // ==========================================

        Guid versaoUsuarioB;

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<ITarefaService>();

            var atualizacao = new Tarefa
            {
                Id = tarefaId,
                Titulo = tituloUsuarioB,
                Descricao = "Atualização válida do usuário B",
                Prioridade = 3,
                Versao = versaoInicial
            };

            var resultado = await service.AtualizarAsync(
                atualizacao);

            Assert.True(resultado);

            var atualizada =
                await service.ObterPorIdAsync(tarefaId);

            Assert.NotNull(atualizada);

            versaoUsuarioB = atualizada.Versao;

            Assert.NotEqual(
                versaoInicial,
                versaoUsuarioB);
        }

        // ==========================================
        // ETAPA 3 - USUARIO A TENTA USAR VERSAO ANTIGA
        // ==========================================

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<ITarefaService>();

            var tentativa = new Tarefa
            {
                Id = tarefaId,
                Titulo = tituloUsuarioA,
                Descricao = "Tentativa com versão desatualizada",
                Prioridade = 1,

                // Token antigo do usuário A.
                Versao = versaoInicial
            };

            // Deve ocorrer uma exceção de concorrência.
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => service.AtualizarAsync(tentativa));
        }

        // ==========================================
        // ETAPA 4 - VERIFICAR O ROLLBACK NO BANCO
        // ==========================================

        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<PortalTarefasDbContext>();

            // Recupera a tarefa diretamente do banco.
            var tarefaPersistida = await db.Tarefas
                .AsNoTracking()
                .SingleAsync(t => t.Id == tarefaId);

            // Recupera todos os eventos de auditoria.
            var eventos = await db.EventosAuditoria
                .AsNoTracking()
                .Where(e => e.TarefaId == tarefaId)
                .ToListAsync();

            // A atualização válida do usuário B permanece.
            Assert.Equal(
                tituloUsuarioB,
                tarefaPersistida.Titulo);

            Assert.Equal(
                versaoUsuarioB,
                tarefaPersistida.Versao);

            // Somente o evento da atualização válida existe.
            Assert.Single(eventos);

            Assert.Equal(
                "ATUALIZACAO",
                eventos[0].Operacao);

            Assert.Contains(
                tituloUsuarioB,
                eventos[0].Descricao);

            // O evento da tentativa inválida não foi persistido.
            Assert.DoesNotContain(
                tituloUsuarioA,
                eventos[0].Descricao);
        }
    }
}
