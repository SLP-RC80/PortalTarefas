using Dapper;
using Npgsql;
using PortalTarefas.Web.Models;

namespace PortalTarefas.Web.Services;

public sealed class TarefaDapperService : ITarefaDapperService
{
    private readonly string _connectionString;

    public TarefaDapperService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Tarefa?> ObterPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "Id",
                "Titulo",
                "Descricao",
                "Prioridade",
                "Prazo",
                "Versao"
            FROM "Tarefas"
            WHERE "Id" = @Id;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<Tarefa>(
            new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
    }
}
