
import type { ReactNode } from 'react';

import {
  Navigate,
  useLocation
} from 'react-router-dom';

import { useAuth } from '../context/AuthContext';

interface ProtectedRouteProps {
  children: ReactNode;
}

export default function ProtectedRoute({
  children
}: ProtectedRouteProps) {

  const { usuario, carregando } = useAuth();
  const location = useLocation();

  if (carregando) {
    return (
      <div className="auth-loading">
        Verificando autenticação...
      </div>
    );
  }

  if (!usuario?.authenticated) {
    return (
      <Navigate
        to="/login"
        state={{ from: location }}
        replace
      />
    );
  }

  return <>{children}</>;
}
