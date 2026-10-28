import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { ApiError, describeError } from '../api/http';
import { retornoSeguro, useAuth } from '../auth/AuthContext';
import { ErrorNotice, Feedback } from '../components/Feedback';

export function Login() {
  const { sessao, estado, erro: erroSessao, restaurar, entrar } = useAuth();
  const [params] = useSearchParams();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState('');

  async function enviar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (salvando) return;
    setSalvando(true);
    setErro('');
    try { await entrar(email.trim(), senha); }
    catch (error) { setErro(error instanceof ApiError && error.status === 401 ? 'E-mail ou senha inválidos. Confira os dados e tente novamente.' : describeError(error)); }
    finally { setSalvando(false); }
  }

  if (estado === 'loading') return <Feedback tipo="loading" titulo="Restaurando sua sessão…" />;
  if (estado === 'error') return <Feedback tipo="error" titulo="Não foi possível verificar sua sessão" mensagem={erroSessao} tentar={restaurar} />;
  if (sessao) return <Navigate to={retornoSeguro(params.get('retorno'))} replace />;

  return <div className="login-layout"><section className="login-story"><p className="eyebrow">MENOS RUÍDO. MAIS CLAREZA.</p><h1 tabIndex={-1}>Dê espaço<br />ao que importa.</h1><p>Um lugar para suas prioridades, sua equipe e o próximo passo.</p><div className="login-illustration" aria-hidden="true"><div className="illustration-line"><i>✓</i><span>Um bom plano.</span></div><div className="illustration-line"><i>✓</i><span>Uma equipe alinhada.</span></div><div className="illustration-line"><i>○</i><span>Seu próximo passo.</span></div></div><span className="story-footnote">PORTAL DE TAREFAS · UNIP</span></section>
    <section className="surface login-card"><p className="eyebrow">BOM TER VOCÊ POR AQUI</p><h2>Entre no seu espaço</h2><p>Use sua conta para criar e acompanhar tarefas.</p><form onSubmit={enviar} aria-busy={salvando}><ErrorNotice erro={erro} /><div className="field"><label htmlFor="email">E-mail</label><input id="email" type="email" autoComplete="username" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} placeholder="seu.email@exemplo.com" /></div><div className="field"><label htmlFor="senha">Senha</label><input id="senha" type="password" autoComplete="current-password" required maxLength={128} value={senha} onChange={event => setSenha(event.target.value)} placeholder="Sua senha" /></div><button className="button button--primary login-submit" disabled={salvando}>{salvando ? 'Entrando…' : 'Entrar'}<span aria-hidden="true">→</span></button></form><Link className="login-back" to="/tarefas">Consultar tarefas públicas ↗</Link></section>
  </div>;
}
