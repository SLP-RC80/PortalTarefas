
export interface AuthUser {
  authenticated: boolean;
  id: string | null;
  email: string | null;
  nomeExibicao: string | null;
  roles: string[];
}

export interface LoginCredentials {
  email: string;
  password: string;
  rememberMe: boolean;
}

interface ApiErrorResponse {
  message?: string;
  detail?: string;
  title?: string;
}

export class AuthApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = 'AuthApiError';
    this.status = status;
  }
}

const ENDPOINT = '/api/auth';

async function requisitar<T>(
  caminho: string,
  options: RequestInit = {}
): Promise<T> {
  const headers = new Headers(options.headers);

  headers.set('Accept', 'application/json');

  if (options.body) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(`${ENDPOINT}${caminho}`, {
    ...options,
    credentials: 'include',
    headers
  });

  if (!response.ok) {
    const problema = await response
      .json()
      .catch(() => null) as ApiErrorResponse | null;

    throw new AuthApiError(
      problema?.message ??
      problema?.detail ??
      problema?.title ??
      `Erro HTTP ${response.status}`,
      response.status
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return await response.json() as T;
}

// Consulta a sessão atual.
export async function consultarSessao(): Promise<AuthUser> {
  return requisitar<AuthUser>('/me');
}

// Realiza login.
export async function realizarLogin(
  credenciais: LoginCredentials
): Promise<AuthUser> {
  return requisitar<AuthUser>('/login', {
    method: 'POST',
    body: JSON.stringify(credenciais)
  });
}

// Encerra a sessão.
export async function realizarLogout(): Promise<void> {
  return requisitar<void>('/logout', {
    method: 'POST'
  });
}
