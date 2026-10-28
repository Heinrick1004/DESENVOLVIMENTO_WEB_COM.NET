import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, Outlet, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { describeError } from './api/http';
import { AuthProvider, ProtectedRoute, useAuth } from './auth/AuthContext';
import { ErrorNotice } from './components/Feedback';
import { TaskList } from './pages/TaskList';
import { TaskDetails } from './pages/TaskDetails';
import { TaskEditor } from './pages/TaskForm';
import { Login } from './pages/Login';

function Layout() {
  const { sessao, estado, sair } = useAuth();
  const [saindo, setSaindo] = useState(false);
  const [erro, setErro] = useState('');
  const navigate = useNavigate();
  const location = useLocation();
  const main = useRef<HTMLElement>(null);
  useEffect(() => {
    setErro('');
    const frame = requestAnimationFrame(() => {
      main.current?.querySelector<HTMLElement>('h1[tabindex="-1"]')?.focus();
      window.scrollTo({ top: 0, behavior: 'instant' });
    });
    return () => cancelAnimationFrame(frame);
  }, [location.pathname]);

  async function logout() {
    if (saindo) return;
    setSaindo(true);
    setErro('');
    try { await sair(); navigate('/tarefas', { replace: true }); }
    catch (error) { setErro(describeError(error)); }
    finally { setSaindo(false); }
  }

  return <><a href="#conteudo" className="skip-link">Pular para o conteúdo</a><header className="site-header"><div className="header-inner"><Link className="brand" to="/tarefas" aria-label="Portal de Tarefas, início"><span className="brand-mark" aria-hidden="true">✓</span><span>Portal<span className="brand-secondary">de Tarefas</span></span></Link><nav aria-label="Navegação principal" className="main-nav"><NavLink to="/tarefas" end>Tarefas</NavLink><NavLink to="/tarefas/nova">Nova tarefa</NavLink></nav><div className="session-area">{estado === 'loading' ? <span className="session-loading" role="status">Verificando sessão…</span> : sessao ? <><div className="user-avatar" aria-hidden="true">{sessao.email.slice(0, 1).toUpperCase()}</div><div className="session-info"><span>{sessao.email}</span><small>{sessao.equipe}</small></div><button className="logout-button" disabled={saindo} onClick={logout}>{saindo ? 'Saindo…' : 'Sair'}</button></> : <Link to={`/entrar?retorno=${encodeURIComponent(location.pathname + location.search)}`} className="button button--small button--ghost">Entrar <span aria-hidden="true">↗</span></Link>}</div></div></header><main id="conteudo" className="main-content" ref={main} tabIndex={-1}><ErrorNotice erro={erro} /><Outlet /></main><footer className="site-footer"><span>Portal de Tarefas</span><p>Desenvolvimento Web com .NET <span aria-hidden="true">·</span> UNIP</p><span>Feito para fazer acontecer.</span></footer></>;
}

export default function App() {
  return <AuthProvider><Routes><Route element={<Layout />}><Route index element={<TaskList />} /><Route path="tarefas" element={<TaskList />} /><Route path="tarefas/nova" element={<ProtectedRoute><TaskEditor /></ProtectedRoute>} /><Route path="tarefas/:id" element={<TaskDetails />} /><Route path="tarefas/:id/editar" element={<ProtectedRoute><TaskEditor /></ProtectedRoute>} /><Route path="entrar" element={<Login />} /><Route path="*" element={<section className="not-found"><p className="eyebrow">404 · CAMINHO NÃO ENCONTRADO</p><h1 tabIndex={-1}>Vamos voltar ao que importa?</h1><p>Esta página não está disponível.</p><Link className="button button--primary" to="/tarefas">Ver minhas tarefas</Link></section>} /></Route></Routes></AuthProvider>;
}
