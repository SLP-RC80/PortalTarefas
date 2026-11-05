
import { useState, type FormEvent } from 'react';

import {
  useLocation,
  useNavigate,
  type Location as RouterLocation
} from 'react-router-dom';

import { useAuth } from '../context/AuthContext';

import './Login.css';

interface EstadoLogin {
  from?: RouterLocation;
}

export default function Login() {

  const { login, carregando } = useAuth();

  const navigate = useNavigate();
  const location = useLocation();

  const estado = location.state as EstadoLogin | null;

  const destino = estado?.from
    ? `${estado.from.pathname}${estado.from.search}${estado.from.hash}`
    : '/';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);

  const [erro, setErro] = useState('');
  const [enviando, setEnviando] = useState(false);

  async function entrar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setErro('');
    setEnviando(true);

    try {
      await login({
        email: email.trim(),
        password,
        rememberMe
      });

      navigate(destino, { replace: true });

    } catch (error) {

      setErro(
        error instanceof Error
          ? error.message
          : 'Não foi possível realizar o login.'
      );

    } finally {
      setEnviando(false);
    }
  }

  if (carregando) {
    return (
      <div className="auth-loading">
        Verificando autenticação...
      </div>
    );
  }

  return (
    <main className="auth-page">

      <section className="auth-card">

        <header className="auth-header">
          <h1>Entrar</h1>
          <p>Portal de Tarefas</p>
        </header>

        <form onSubmit={entrar} className="auth-form">

          <p className="auth-description">
            Informe suas credenciais para acessar o sistema.
          </p>

          {erro && (
            <div className="auth-error" role="alert">
              {erro}
            </div>
          )}

          <div className="auth-field">

            <label htmlFor="email">
              E-mail
            </label>

            <input
              id="email"
              type="email"
              value={email}
              onChange={event => setEmail(event.target.value)}
              placeholder="seu@email.com"
              autoComplete="username"
              required
            />

          </div>

          <div className="auth-field">

            <label htmlFor="password">
              Senha
            </label>

            <input
              id="password"
              type="password"
              value={password}
              onChange={event => setPassword(event.target.value)}
              placeholder="Digite sua senha"
              autoComplete="current-password"
              required
            />

          </div>

          <label className="auth-remember">

            <input
              type="checkbox"
              checked={rememberMe}
              onChange={event =>
                setRememberMe(event.target.checked)
              }
            />

            Manter conectado

          </label>

          <button
            type="submit"
            className="auth-submit"
            disabled={enviando}
          >
            {enviando ? 'Entrando...' : 'Entrar no sistema'}
          </button>

        </form>

      </section>

    </main>
  );
}
