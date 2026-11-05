
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import {
  BrowserRouter,
  Navigate,
  Route,
  Routes
} from 'react-router-dom';

import './index.css';

import App from './App';
import Login from './pages/Login';

import { AuthProvider } from './context/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';

createRoot(document.getElementById('root')!).render(
  <StrictMode>

    <BrowserRouter>

      <AuthProvider>

        <Routes>

          {/* Tela de login */}
          <Route
            path="/login"
            element={<Login />}
          />

          {/* Painel protegido */}
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <App />
              </ProtectedRoute>
            }
          />

          {/* Rotas desconhecidas */}
          <Route
            path="*"
            element={<Navigate to="/" replace />}
          />

        </Routes>

      </AuthProvider>

    </BrowserRouter>

  </StrictMode>
);
