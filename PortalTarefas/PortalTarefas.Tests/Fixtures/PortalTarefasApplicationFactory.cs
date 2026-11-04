
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PortalTarefas.Web.Data;

namespace PortalTarefas.Tests.Fixtures;

public sealed class PortalTarefasApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly string? _previousDatabaseUrl;

    // Provedor de logs utilizado pelos testes de auditoria.
    public TestLoggerProvider LoggerProvider { get; } = new();

    public PortalTarefasApplicationFactory()
    {
        // URL fictícia: impede que os testes utilizem o Neon.
        _previousDatabaseUrl =
            Environment.GetEnvironmentVariable("DATABASE_URL");

        Environment.SetEnvironmentVariable(
            "DATABASE_URL",
            "postgresql://test:test@localhost:5432/testdb");

        // Banco SQLite mantido em memória durante os testes.
        _connection = new SqliteConnection(
            "Data Source=:memory:");

        _connection.Open();

        // Cria antecipadamente a estrutura do banco.
        var options =
            new DbContextOptionsBuilder<PortalTarefasDbContext>()
                .UseSqlite(_connection)
                .Options;

        using var context =
            new PortalTarefasDbContext(options);

        context.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Configura a captura dos logs de auditoria.
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();

            logging.AddProvider(LoggerProvider);

            logging.SetMinimumLevel(LogLevel.Information);
        });

        builder.ConfigureTestServices(services =>
        {
            // Permite testar a validação executada
            // diretamente dentro do Controller.
            services.Configure<ApiBehaviorOptions>(
                options =>
                {
                    options.SuppressModelStateInvalidFilter = true;
                });

            // Remove as configurações originais do PostgreSQL.
            services.RemoveAll<
                DbContextOptions<PortalTarefasDbContext>>();

            services.RemoveAll<DbContextOptions>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<
                    PortalTarefasDbContext>>();

            services.RemoveAll<PortalTarefasDbContext>();

            // Registra SQLite exclusivamente para os testes.
            services.AddDbContext<PortalTarefasDbContext>(
                options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();

            LoggerProvider.Clear();

            Environment.SetEnvironmentVariable(
                "DATABASE_URL",
                _previousDatabaseUrl);
        }
    }

    // =====================================================
    // PROVEDOR DE LOGS EM MEMÓRIA
    // =====================================================

    public sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<TestLogEntry> _entries =
            new();

        // Retorna os registros capturados.
        public IReadOnlyCollection<TestLogEntry> Entries =>
            _entries.ToArray();

        // Remove os registros anteriores.
        public void Clear()
        {
            while (_entries.TryDequeue(out _))
            {
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestLogger(categoryName, this);
        }

        internal void Add(TestLogEntry entry)
        {
            _entries.Enqueue(entry);
        }

        public void Dispose()
        {
            // Não descarta o provedor, pois ele pertence
            // à instância da ApplicationFactory.
        }

        // =================================================
        // LOGGER
        // =================================================

        private sealed class TestLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly TestLoggerProvider _provider;

            public TestLogger(
                string categoryName,
                TestLoggerProvider provider)
            {
                _categoryName = categoryName;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                where TState : notnull
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var mensagem = formatter(state, exception);

                _provider.Add(
                    new TestLogEntry(
                        _categoryName,
                        mensagem,
                        logLevel,
                        eventId,
                        exception));
            }
        }

        // =================================================
        // ESCOPO VAZIO
        // =================================================

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    // =====================================================
    // MODELO DO REGISTRO DE LOG
    // =====================================================

    public sealed record TestLogEntry(
        string Category,
        string Message,
        LogLevel LogLevel,
        EventId EventId,
        Exception? Exception);
}
