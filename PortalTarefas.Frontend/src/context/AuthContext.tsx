
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from 'react';

import {
  AuthApiError,
  consultarSessao,
  realizarLogin,
  realizarLogout,
  type AuthUser,
  type LoginCredentials
} from '../api/auth';

interface AuthContextType {
  usuario: AuthUser | null;
  carregando: boolean;
  isAdmin: boolean;

  login: (dados: LoginCredentials) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(
  undefined
);

export function AuthProvider({
  children
}: {
  children: ReactNode;
}) {
  const [usuario, setUsuario] = useState<AuthUser | null>(null);
  const [carregando, setCarregando] = useState(true);

  // Recupera a sessão ao iniciar a aplicação.
  useEffect(() => {
    let ativo = true;

    async function verificarSessao() {
      try {
        const resultado = await consultarSessao();

        if (ativo) {
          setUsuario(
            resultado.authenticated ? resultado : null
          );
        }
      } catch {
        if (ativo) {
          setUsuario(null);
        }
      } finally {
        if (ativo) {
          setCarregando(false);
        }
      }
    }

    void verificarSessao();

    return () => {
      ativo = false;
    };
  }, []);

  // Autentica o usuário.
  const login = useCallback(
    async (dados: LoginCredentials) => {
      const resultado = await realizarLogin(dados);

      if (!resultado.authenticated) {
        throw new Error('Não foi possível autenticar o usuário.');
      }

      setUsuario(resultado);
    },
    []
  );

  // Encerra a sessão.
const logout = useCallback(async () => {
  try {
    await realizarLogout();
  } catch (error) {
    if (!(error instanceof AuthApiError) || error.status !== 401) {
      throw error;
    }
  }

  setUsuario(null);
}, []);

  // Identifica o administrador pelo papel retornado pelo backend.
  const isAdmin = usuario?.roles.includes('Administrador') ?? false;

  const valor = useMemo(
    () => ({
      usuario,
      carregando,
      isAdmin,
      login,
      logout
    }),
    [usuario, carregando, isAdmin, login, logout]
  );

  return (
    <AuthContext.Provider value={valor}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const contexto = useContext(AuthContext);

  if (!contexto) {
    throw new Error(
      'useAuth deve ser utilizado dentro de AuthProvider.'
    );
  }

  return contexto;
}
