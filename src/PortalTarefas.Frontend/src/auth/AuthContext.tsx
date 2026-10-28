import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { api } from '../api/client';
import { ApiError, describeError, isAbort } from '../api/http';
import type { DetalheTarefa, Sessao } from '../api/types';
import { Feedback } from '../components/Feedback';

interface AuthValue {
  sessao: Sessao | null;
  estado: 'loading' | 'success' | 'error';
  erro: string;
  restaurar: () => void;
  entrar: (email: string, senha: string) => Promise<void>;
  sair: () => Promise<void>;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sessao, setSessao] = useState<Sessao | null>(null);
  const [estado, setEstado] = useState<AuthValue['estado']>('loading');
  const [erro, setErro] = useState('');
  const [tentativa, setTentativa] = useState(0);
  const restaurar = useCallback(() => setTentativa(value => value + 1), []);

  useEffect(() => {
    const abort = new AbortController();
    setEstado('loading');
    api.sessao(abort.signal).then(data => {
      if (!abort.signal.aborted) { setSessao(data); setEstado('success'); }
    }).catch(error => {
      if (isAbort(error) || abort.signal.aborted) return;
      setSessao(null);
      if (error instanceof ApiError && error.status === 401) setEstado('success');
      else { setErro(describeError(error)); setEstado('error'); }
    });
    return () => abort.abort();
  }, [tentativa]);

  useEffect(() => {
    const expire = () => { setSessao(null); };
    window.addEventListener('session-expired', expire);
    return () => window.removeEventListener('session-expired', expire);
  }, []);

  async function entrar(email: string, senha: string) {
    const data = await api.entrar(email, senha);
    setSessao(data);
    setEstado('success');
  }

  async function sair() {
    await api.sair();
    setSessao(null);
  }

  return <AuthContext value={{ sessao, estado, erro, restaurar, entrar, sair }}>{children}</AuthContext>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error('AuthProvider ausente.');
  return value;
}

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { sessao, estado, erro, restaurar } = useAuth();
  const location = useLocation();
  if (estado === 'loading') return <Feedback tipo="loading" titulo="Restaurando sua sessão…" />;
  if (estado === 'error') return <Feedback tipo="error" titulo="Não foi possível verificar sua sessão" mensagem={erro} tentar={restaurar} />;
  if (!sessao) return <Navigate to={`/entrar?retorno=${encodeURIComponent(location.pathname + location.search)}`} replace />;
  return children;
}

export function podeEditar(sessao: Sessao, tarefa: DetalheTarefa): boolean {
  return tarefa.proprietarioId === sessao.id || tarefa.equipe === sessao.equipe || sessao.podeEditarOutrasEquipes;
}

export function retornoSeguro(value: string | null): string {
  return value?.startsWith('/') && !value.startsWith('//') && !value.includes('\\') && !value.startsWith('/entrar')
    ? value : '/tarefas';
}
