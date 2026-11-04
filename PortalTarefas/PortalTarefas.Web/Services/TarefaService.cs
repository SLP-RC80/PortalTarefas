using Microsoft.EntityFrameworkCore;
using PortalTarefas.Web.Data;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

public sealed class TarefaService : ITarefaService
{
    private readonly PortalTarefasDbContext _context;

    public TarefaService(PortalTarefasDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Tarefa>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Tarefas
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tarefa?> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tarefas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);
    }

    public async Task<Tarefa> CriarAsync(
        Tarefa tarefa,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tarefa);

        await _context.Tarefas.AddAsync(
            tarefa,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return tarefa;
    }

    // Atualização com auditoria e transação.

// Atualização com auditoria, transação e controle de concorrência.
public async Task<bool> AtualizarAsync(
    Tarefa tarefa,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(tarefa);

    await using var transaction =
        await _context.Database.BeginTransactionAsync(
            cancellationToken);

    try
    {
        var existente = await _context.Tarefas
            .FirstOrDefaultAsync(
                t => t.Id == tarefa.Id,
                cancellationToken);

        if (existente == null)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            return false;
        }

        // Informa ao EF qual era a versão recebida pelo formulário.
        _context.Entry(existente)
            .Property(t => t.Versao)
            .OriginalValue = tarefa.Versao;

        // Atualiza os dados.
        existente.Titulo = tarefa.Titulo;
        existente.Descricao = tarefa.Descricao;
        existente.Prioridade = tarefa.Prioridade;
        existente.Prazo = tarefa.Prazo;

        // Gera um novo token para a próxima edição.
        existente.Versao = Guid.NewGuid();

        // Registra a operação de auditoria.
        var evento = new EventoAuditoria
        {
            TarefaId = existente.Id,
            Operacao = "ATUALIZACAO",
            Descricao = $"Tarefa '{existente.Titulo}' atualizada.",
            DataHoraUtc = DateTimeOffset.UtcNow
        };

        await _context.EventosAuditoria.AddAsync(
            evento,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }
    catch
    {
        await transaction.RollbackAsync(
            CancellationToken.None);

        throw;
    }
}

}