
export type Prioridade = 1 | 2 | 3;

export interface Tarefa {
  id: number;
  titulo: string;
  descricao: string | null;
  prioridade: Prioridade;
  prazo: string | null;
  versao: string;
}

export interface TarefaListagem {
  pagina: number;
  tamanhoPagina: number;
  totalRegistros: number;
  totalPaginas: number;
  tarefas: Tarefa[];
}

export interface TarefaInput {
  titulo: string;
  descricao?: string | null;
  prioridade: Prioridade;
  prazo?: string | null;
  versao?: string;
}

export interface ProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  errors?: Record<string, string[]>;
}
