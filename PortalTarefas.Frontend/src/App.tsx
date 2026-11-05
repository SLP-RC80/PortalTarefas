
import { useAuth } from './context/AuthContext';
import { useEffect, useState, type FormEvent } from 'react';

import {
  ApiError,
  listarTarefas,
  criarTarefa,
  atualizarTarefa
} from './api/tarefas';

import type {
  Tarefa,
  TarefaInput,
  TarefaListagem,
  Prioridade
} from './types/tarefa';

import './App.css';

interface FormularioTarefa {
  titulo: string;
  descricao: string;
  prioridade: Prioridade;
  prazo: string;
}

const formularioInicial: FormularioTarefa = {
  titulo: '',
  descricao: '',
  prioridade: 2,
  prazo: ''
};

function formatarData(data: string | null): string {
  if (!data) return 'Sem prazo';

  const [ano, mes, dia] = data.slice(0, 10).split('-');

  return `${dia}/${mes}/${ano}`;
}

function nomePrioridade(prioridade: Prioridade): string {
  const nomes: Record<Prioridade, string> = {
    1: 'Baixa',
    2: 'Média',
    3: 'Alta'
  };

  return nomes[prioridade];
}

function App() {

  // ==========================================
  // AUTENTICACAO E PERFIL
  // ==========================================

  const { usuario, isAdmin, logout } = useAuth();

  const [saindo, setSaindo] = useState(false);

  // ==========================================
  // ESTADOS DA APLICACAO
  // ==========================================

  const [resultado, setResultado] =
    useState<TarefaListagem | null>(null);

  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');
  const [erroListagem, setErroListagem] = useState('');
  const [aviso, setAviso] = useState('');

  const [termo, setTermo] = useState('');
  const [termoAplicado, setTermoAplicado] = useState('');

  const [pagina, setPagina] = useState(1);
  const [atualizacao, setAtualizacao] = useState(0);

  const [modalAberto, setModalAberto] = useState(false);
  const [salvando, setSalvando] = useState(false);

  const [tarefaEmEdicao, setTarefaEmEdicao] =
    useState<Tarefa | null>(null);

  const [formulario, setFormulario] =
    useState<FormularioTarefa>(formularioInicial);

  const [errosCampo, setErrosCampo] =
    useState<Record<string, string[]>>({});

  // ==========================================
  // CARREGAMENTO DA LISTAGEM
  // ==========================================

  useEffect(() => {
    const controller = new AbortController();

    async function carregarTarefas() {
      setCarregando(true);
      setErroListagem('');

      try {
        const dados = await listarTarefas(
          {
            termo: termoAplicado || undefined,
            pagina,
            tamanhoPagina: 10
          },
          controller.signal
        );

        setResultado(dados);

      } catch (error) {

        if (
          error instanceof Error &&
          error.name === 'AbortError'
        ) {
          return;
        }

        setErroListagem(
          error instanceof Error
            ? error.message
            : 'Erro ao carregar as tarefas.'
        );

        setResultado(null);

      } finally {

        if (!controller.signal.aborted) {
          setCarregando(false);
        }
      }
    }

    void carregarTarefas();

    return () => controller.abort();

  }, [pagina, termoAplicado, atualizacao]);

  // ==========================================
  // LOGOUT
  // ==========================================

  async function sair() {
    setSaindo(true);
    setErroListagem('');

    try {
      await logout();

    } catch (error) {

      setErroListagem(
        error instanceof Error
          ? error.message
          : 'Não foi possível encerrar a sessão.'
      );

    } finally {
      setSaindo(false);
    }
  }

  // ==========================================
  // PESQUISA
  // ==========================================

  function pesquisar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setPagina(1);
    setTermoAplicado(termo.trim());
  }

  // ==========================================
  // ABERTURA DO FORMULARIO
  // ==========================================

  function abrirCadastro() {

    // Somente administradores podem cadastrar.
    if (!isAdmin) return;

    setTarefaEmEdicao(null);
    setFormulario({ ...formularioInicial });
    setErrosCampo({});
    setErro('');
    setModalAberto(true);
  }

  function abrirEdicao(tarefa: Tarefa) {

    // Somente administradores podem editar.
    if (!isAdmin) return;

    setTarefaEmEdicao(tarefa);

    setFormulario({
      titulo: tarefa.titulo,
      descricao: tarefa.descricao ?? '',
      prioridade: tarefa.prioridade,
      prazo: tarefa.prazo?.slice(0, 10) ?? ''
    });

    setErrosCampo({});
    setErro('');
    setModalAberto(true);
  }

  function fecharModal() {
    if (salvando) return;

    setModalAberto(false);
    setTarefaEmEdicao(null);
    setErro('');
  }

  // ==========================================
  // ATUALIZACAO DOS CAMPOS
  // ==========================================

  function atualizarCampo<K extends keyof FormularioTarefa>(
    campo: K,
    valor: FormularioTarefa[K]
  ) {
    setFormulario(atual => ({
      ...atual,
      [campo]: valor
    }));
  }

  function obterErroCampo(nome: string): string {
    const chave = Object.keys(errosCampo).find(
      item => item.toLowerCase() === nome.toLowerCase()
    );

    return chave
      ? errosCampo[chave]?.[0] ?? ''
      : '';
  }

  // ==========================================
  // CADASTRO E ATUALIZACAO
  // ==========================================

  async function salvarTarefa(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    setErro('');
    setErrosCampo({});
    setAviso('');

    // Protecao adicional da interface.
    if (!isAdmin) {
      setErro(
        'Seu perfil não possui permissão para gerenciar tarefas.'
      );
      return;
    }

    // Validacao do titulo.
    if (formulario.titulo.trim().length < 3) {
      setErro(
        'O título deve possuir pelo menos 3 caracteres.'
      );
      return;
    }

    if (formulario.titulo.trim().length > 120) {
      setErro(
        'O título deve possuir no máximo 120 caracteres.'
      );
      return;
    }

    // Validacao da descricao.
    if (formulario.descricao.length > 500) {
      setErro(
        'A descrição deve possuir no máximo 500 caracteres.'
      );
      return;
    }

    setSalvando(true);

    const dados: TarefaInput = {
      titulo: formulario.titulo.trim(),
      descricao: formulario.descricao.trim() || null,
      prioridade: formulario.prioridade,
      prazo: formulario.prazo
        ? `${formulario.prazo}T00:00:00`
        : null,

      ...(tarefaEmEdicao
        ? { versao: tarefaEmEdicao.versao }
        : {})
    };

    try {

      // Atualizacao de tarefa.
      if (tarefaEmEdicao) {

        await atualizarTarefa(
          tarefaEmEdicao.id,
          dados
        );

        setAviso(
          'Tarefa atualizada com sucesso!'
        );

      } else {

        // Cadastro de nova tarefa.
        await criarTarefa(dados);

        setAviso(
          'Tarefa cadastrada com sucesso!'
        );

        setPagina(1);
      }

      setModalAberto(false);
      setTarefaEmEdicao(null);

      setAtualizacao(valor => valor + 1);

    } catch (error) {

      if (error instanceof ApiError) {

        // Conflito de concorrencia.
        if (error.status === 409) {

          setErro(
            'Conflito de concorrência: esta tarefa foi modificada por outro usuário. Atualize a listagem e tente novamente.'
          );

          setAtualizacao(valor => valor + 1);

        // Registro nao encontrado.
        } else if (error.status === 404) {

          setErro(
            'A tarefa não foi encontrada. Atualize a listagem.'
          );

          setAtualizacao(valor => valor + 1);

        // Erros de validacao da API.
        } else if (error.errors) {

          setErrosCampo(error.errors);
          setErro(error.message);

        } else {

          setErro(error.message);
        }

      } else {

        setErro(
          error instanceof Error
            ? error.message
            : 'Não foi possível salvar a tarefa.'
        );
      }

    } finally {
      setSalvando(false);
    }
  }

  // ==========================================
  // DADOS DA INTERFACE
  // ==========================================

  const tarefas = resultado?.tarefas ?? [];

  const totalRegistros =
    resultado?.totalRegistros ?? 0;

  const totalPaginas = Math.max(
    1,
    resultado?.totalPaginas ?? 1
  );

  const paginaAtual =
    resultado?.pagina ?? pagina;

  // ==========================================
  // INTERFACE
  // ==========================================

  return (

    <div className="app-shell">

      {/* =====================================
          CABECALHO
          ===================================== */}

      <header className="topbar">

        <div className="topbar-content">

          <a href="/" className="brand">

            <span className="brand-icon">
              ✓
            </span>

            <span>
              <strong>Portal de Tarefas</strong>
              <small>Gestão de atividades</small>
            </span>

          </a>

          {/* IDENTIFICACAO E SESSAO */}

          <div className="topbar-account">

            <span className="connection-status">
              <span className="status-dot"></span>
              Aplicação online
            </span>

            <div className="account-panel">

              <div className="account-identity">

                <strong>
                  {usuario?.nomeExibicao || 'Usuário'}
                </strong>

                <small>
                  {usuario?.email}
                </small>

              </div>

              <span
                className={`account-role ${
                  isAdmin
                    ? 'account-role-admin'
                    : 'account-role-user'
                }`}
              >
                {isAdmin
                  ? 'Administrador'
                  : 'Usuário'}
              </span>

              <button
                type="button"
                className="button button-outline account-logout"
                onClick={() => void sair()}
                disabled={saindo}
              >
                {saindo ? 'Saindo...' : 'Sair'}
              </button>

            </div>

          </div>

        </div>

      </header>

      {/* =====================================
          CONTEUDO PRINCIPAL
          ===================================== */}

      <main className="page-container">

        {/* APRESENTACAO */}

        <section className="welcome-section">

          <div>

            <span className="eyebrow">
              PAINEL DE CONTROLE
            </span>

            <h1>Minhas tarefas</h1>

            <p>
              Organize, acompanhe e gerencie suas atividades.
            </p>

          </div>

          {/* NOVA TAREFA - SOMENTE ADMINISTRADOR */}

          {isAdmin && (

            <button
              className="button button-primary"
              onClick={abrirCadastro}
            >
              <span>＋</span>
              Nova tarefa
            </button>

          )}

        </section>

        {/* =====================================
            INDICADORES
            ===================================== */}

        <section className="stats-grid">

          <article className="stat-card">

            <span className="stat-label">
              Total de tarefas
            </span>

            <strong>
              {totalRegistros}
            </strong>

            <span className="stat-description">
              Registros cadastrados
            </span>

          </article>

          <article className="stat-card">

            <span className="stat-label">
              Página atual
            </span>

            <strong>
              {paginaAtual}
            </strong>

            <span className="stat-description">
              De {totalPaginas} página(s)
            </span>

          </article>

          <article className="stat-card">

            <span className="stat-label">
              Exibindo
            </span>

            <strong>
              {tarefas.length}
            </strong>

            <span className="stat-description">
              Registros nesta página
            </span>

          </article>

        </section>

        {/* =====================================
            LISTAGEM DE TAREFAS
            ===================================== */}

        <section className="content-card">

          <div className="list-header">

            <div>

              <h2>Lista de atividades</h2>

              <p>
                {isAdmin
                  ? 'Consulte e gerencie suas tarefas cadastradas.'
                  : 'Consulte suas tarefas cadastradas.'}
              </p>

            </div>

            {/* PESQUISA */}

            <form
              className="search-form"
              onSubmit={pesquisar}
            >

              <input
                type="search"
                placeholder="Pesquisar tarefa..."
                value={termo}
                onChange={event =>
                  setTermo(event.target.value)
                }
              />

              <button
                type="submit"
                className="button button-search"
              >
                Pesquisar
              </button>

            </form>

          </div>

          {/* MENSAGEM DE SUCESSO */}

          {aviso && (

            <div className="alert alert-success">

              {aviso}

              <button
                onClick={() => setAviso('')}
                aria-label="Fechar aviso"
              >
                ×
              </button>

            </div>

          )}

          {/* ERRO DA LISTAGEM */}

          {erroListagem && (

            <div className="alert alert-error">

              {erroListagem}

              <button
                onClick={() => setErroListagem('')}
                aria-label="Fechar erro"
              >
                ×
              </button>

            </div>

          )}

          {/* CARREGAMENTO */}

          {carregando ? (

            <div className="loading-state">

              <div className="spinner"></div>

              <p>Carregando tarefas...</p>

            </div>

          ) : erroListagem && !resultado ? (

            /* ERRO AO CONSULTAR A API */

            <div className="empty-state">

              <h3>
                Não foi possível carregar os dados
              </h3>

              <p>
                {erroListagem}
              </p>

              <button
                className="button button-primary"
                onClick={() =>
                  setAtualizacao(valor => valor + 1)
                }
              >
                Tentar novamente
              </button>

            </div>

          ) : tarefas.length === 0 ? (

            /* LISTAGEM VAZIA */

            <div className="empty-state">

              <span className="empty-icon">
                □
              </span>

              <h3>
                Nenhuma tarefa encontrada
              </h3>

              <p>
                {isAdmin
                  ? 'Cadastre uma nova tarefa ou altere os critérios de pesquisa.'
                  : 'Não existem tarefas disponíveis para os critérios de pesquisa.'}
              </p>

              {isAdmin && (

                <button
                  className="button button-primary"
                  onClick={abrirCadastro}
                >
                  Nova tarefa
                </button>

              )}

            </div>

          ) : (

            /* LISTAGEM DE REGISTROS */

            <div className="task-list">

              {tarefas.map(tarefa => (

                <article
                  className="task-card"
                  key={tarefa.id}
                >

                  <div className="task-main">

                    <div className="task-title-row">

                      <span className="task-id">
                        #{tarefa.id}
                      </span>

                      <h3>
                        {tarefa.titulo}
                      </h3>

                    </div>

                    <p className="task-description">
                      {tarefa.descricao ||
                        'Sem descrição cadastrada.'}
                    </p>

                    <div className="task-metadata">

                      <span
                        className={`priority-badge priority-${tarefa.prioridade}`}
                      >
                        {nomePrioridade(tarefa.prioridade)}
                      </span>

                      <span className="task-date">

                        <span>◷</span>

                        Prazo: {formatarData(tarefa.prazo)}

                      </span>

                    </div>

                  </div>

                  {/* EDICAO - SOMENTE ADMINISTRADOR */}

                  {isAdmin && (

                    <div className="task-actions">

                      <button
                        className="button button-outline"
                        onClick={() => abrirEdicao(tarefa)}
                      >
                        Editar
                      </button>

                    </div>

                  )}

                </article>

              ))}

            </div>

          )}

          {/* =====================================
              PAGINACAO
              ===================================== */}

          {!carregando && resultado && (

            <div className="pagination">

              <span>
                Página {paginaAtual} de {totalPaginas}
              </span>

              <div className="pagination-actions">

                <button
                  className="button button-outline"
                  disabled={paginaAtual <= 1}
                  onClick={() =>
                    setPagina(valor => valor - 1)
                  }
                >
                  Anterior
                </button>

                <button
                  className="button button-outline"
                  disabled={paginaAtual >= totalPaginas}
                  onClick={() =>
                    setPagina(valor => valor + 1)
                  }
                >
                  Próxima
                </button>

              </div>

            </div>

          )}

        </section>

        {/* RODAPE */}

        <footer className="footer">
          Portal de Tarefas · ASP.NET Core · React · PostgreSQL
        </footer>

      </main>

      {/* =====================================
          MODAL DE CADASTRO E EDICAO
          ===================================== */}

      {modalAberto && isAdmin && (

        <div className="modal-backdrop">

          <section
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-title"
          >

            {/* CABECALHO DO MODAL */}

            <div className="modal-header">

              <div>

                <span className="eyebrow">
                  GERENCIAMENTO
                </span>

                <h2 id="modal-title">

                  {tarefaEmEdicao
                    ? 'Editar tarefa'
                    : 'Nova tarefa'}

                </h2>

              </div>

              <button
                className="modal-close"
                onClick={fecharModal}
                disabled={salvando}
                aria-label="Fechar formulário"
              >
                ×
              </button>

            </div>

            {/* ERRO DO FORMULARIO */}

            {erro && (

              <div className="alert alert-error">
                {erro}
              </div>

            )}

            {/* FORMULARIO */}

            <form
              className="task-form"
              onSubmit={salvarTarefa}
            >

              {/* TITULO */}

              <div className="form-field">

                <label htmlFor="titulo">
                  Título *
                </label>

                <input
                  id="titulo"
                  type="text"
                  minLength={3}
                  maxLength={120}
                  required
                  value={formulario.titulo}
                  onChange={event =>
                    atualizarCampo(
                      'titulo',
                      event.target.value
                    )
                  }
                  placeholder="Digite o título da tarefa"
                />

                {obterErroCampo('titulo') && (

                  <small className="field-error">
                    {obterErroCampo('titulo')}
                  </small>

                )}

              </div>

              {/* DESCRICAO */}

              <div className="form-field">

                <label htmlFor="descricao">
                  Descrição
                </label>

                <textarea
                  id="descricao"
                  maxLength={500}
                  rows={4}
                  value={formulario.descricao}
                  onChange={event =>
                    atualizarCampo(
                      'descricao',
                      event.target.value
                    )
                  }
                  placeholder="Descreva a atividade..."
                />

                <small className="field-hint">
                  Máximo de 500 caracteres.
                </small>

              </div>

              {/* PRIORIDADE E PRAZO */}

              <div className="form-grid">

                <div className="form-field">

                  <label htmlFor="prioridade">
                    Prioridade *
                  </label>

                  <select
                    id="prioridade"
                    value={formulario.prioridade}
                    onChange={event =>
                      atualizarCampo(
                        'prioridade',
                        Number(event.target.value) as Prioridade
                      )
                    }
                  >
                    <option value={1}>Baixa</option>
                    <option value={2}>Média</option>
                    <option value={3}>Alta</option>
                  </select>

                </div>

                <div className="form-field">

                  <label htmlFor="prazo">
                    Prazo
                  </label>

                  <input
                    id="prazo"
                    type="date"
                    value={formulario.prazo}
                    onChange={event =>
                      atualizarCampo(
                        'prazo',
                        event.target.value
                      )
                    }
                  />

                </div>

              </div>

              {/* BOTOES */}

              <div className="modal-actions">

                <button
                  type="button"
                  className="button button-outline"
                  onClick={fecharModal}
                  disabled={salvando}
                >
                  Cancelar
                </button>

                <button
                  type="submit"
                  className="button button-primary"
                  disabled={salvando}
                >
                  {salvando
                    ? 'Salvando...'
                    : tarefaEmEdicao
                      ? 'Salvar alterações'
                      : 'Cadastrar tarefa'}
                </button>

              </div>

            </form>

          </section>

        </div>

      )}

    </div>
  );
}

export default App;
