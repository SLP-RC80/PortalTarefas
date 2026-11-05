
import type {
  Tarefa,
  TarefaInput,
  TarefaListagem,
  ProblemDetails
} from '../types/tarefa';

const ENDPOINT = '/api/v1/tarefas';


export class ApiError extends Error {
  readonly status: number;
  readonly errors?: Record<string, string[]>;

  constructor(
    message: string,
    status: number,
    errors?: Record<string, string[]>
  ) {
    super(message);

    this.name = 'ApiError';
    this.status = status;
    this.errors = errors;
  }
}


async function requisitar<T>(
  url: string,
  options: RequestInit = {}
): Promise<T> {
  const headers = new Headers(options.headers);

  headers.set('Accept', 'application/json');

  if (options.body) {
    headers.set('Content-Type', 'application/json');
  }

  let response: Response;

  try {
    response = await fetch(url, {
        ...options,
        credentials: 'include',
        headers
    });
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      throw error;
    }

    throw new Error(
      'Não foi possível conectar à API. Verifique a conexão.'
    );
  }

  if (!response.ok) {
    const problema = await response
      .json()
      .catch(() => null) as ProblemDetails | null;

    throw new ApiError(
      problema?.detail ??
      problema?.title ??
      `Erro HTTP ${response.status}`,
      response.status,
      problema?.errors
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return await response.json() as T;
}

// Listagem com filtros e paginação.
export async function listarTarefas(
  parametros: {
    termo?: string;
    pagina?: number;
    tamanhoPagina?: number;
  } = {},
  signal?: AbortSignal
): Promise<TarefaListagem> {
  const query = new URLSearchParams();

  if (parametros.termo?.trim()) {
    query.set('termo', parametros.termo.trim());
  }

  query.set('pagina', String(parametros.pagina ?? 1));

  query.set(
    'tamanhoPagina',
    String(parametros.tamanhoPagina ?? 10)
  );

  return requisitar<TarefaListagem>(
    `${ENDPOINT}?${query.toString()}`,
    { signal }
  );
}

// Consulta de tarefa individual.
export async function obterTarefa(
  id: number,
  signal?: AbortSignal
): Promise<Tarefa> {
  return requisitar<Tarefa>(
    `${ENDPOINT}/${id}`,
    { signal }
  );
}

// Cadastro de tarefa.
export async function criarTarefa(
  tarefa: TarefaInput,
  signal?: AbortSignal
): Promise<Tarefa> {
  return requisitar<Tarefa>(
    ENDPOINT,
    {
      method: 'POST',
      body: JSON.stringify(tarefa),
      signal
    }
  );
}

// Atualização de tarefa.
export async function atualizarTarefa(
  id: number,
  tarefa: TarefaInput,
  signal?: AbortSignal
): Promise<Tarefa> {
  return requisitar<Tarefa>(
    `${ENDPOINT}/${id}`,
    {
      method: 'PUT',
      body: JSON.stringify(tarefa),
      signal
    }
  );
}
