import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import { ApiError, describeError, isAbort } from '../api/http';
import { podeEditar, useAuth } from '../auth/AuthContext';
import { useAsync } from '../components/AsyncState';
import { ErrorNotice, Feedback } from '../components/Feedback';
import { formatDate, prioridades, TaskBadge } from '../components/TaskBadge';
import { listaRetorno, queryRetorno } from './navigation';

export function TaskDetails() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const voltar = listaRetorno(params);
  const { sessao } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const load = useCallback((signal: AbortSignal) => Number.isSafeInteger(Number(id)) && Number(id) > 0 ? api.detalhes(Number(id), signal) : Promise.reject(new ApiError('http', 404)), [id]);
  const { state, recarregar } = useAsync(load);
  const [confirmar, setConfirmar] = useState(false);
  const [excluindo, setExcluindo] = useState(false);
  const [erro, setErro] = useState('');
  const dialog = useRef<HTMLDialogElement>(null);
  const mutation = useRef<AbortController | null>(null);
  useEffect(() => () => mutation.current?.abort(), []);
  useEffect(() => { if (confirmar) dialog.current?.showModal(); else dialog.current?.close(); }, [confirmar]);

  async function excluir() {
    if ((state.estado !== 'success' && state.estado !== 'empty') || excluindo) return;
    const controller = new AbortController();
    mutation.current = controller;
    setExcluindo(true);
    setErro('');
    try {
      await api.excluir(state.data.id, state.data.rowVersion, controller.signal);
      navigate(voltar, { replace: true, state: { mensagem: 'Tarefa excluída com sucesso.' } });
    } catch (error) {
      if (!isAbort(error)) { setErro(describeError(error)); setConfirmar(false); }
    } finally { if (!controller.signal.aborted) setExcluindo(false); }
  }

  if (state.estado === 'idle' || state.estado === 'loading') return <Feedback tipo={state.estado} titulo="Carregando detalhes…" />;
  if (state.estado === 'error') return <><Link className="back-link" to={voltar}>← Voltar às tarefas</Link><Feedback tipo="error" titulo="Não foi possível abrir a tarefa" mensagem={state.erro} tentar={recarregar} /></>;
  const tarefa = state.data;
  const admin = sessao?.roles.includes('Administrador');
  const editar = sessao && podeEditar(sessao, tarefa);
  const mensagem = (location.state as { mensagem?: string } | null)?.mensagem;

  return <>
    <Link className="back-link" to={voltar}>← Voltar às tarefas</Link>
    <div className="page-heading detail-heading"><div><p className="eyebrow">TAREFA #{String(tarefa.id).padStart(3, '0')}</p><h1 tabIndex={-1}>{tarefa.titulo}</h1><p className="page-description">Criada em {formatDate(tarefa.criadaEm)} · Equipe {tarefa.equipe}</p></div><TaskBadge situacao={tarefa.situacao} /></div>
    {mensagem && <div className="notice notice--success" role="status">{mensagem}</div>}
    <ErrorNotice erro={erro} />
    <div className="detail-grid"><article className="surface detail-body"><p className="eyebrow">SOBRE A TAREFA</p><h2>O que precisa ser feito</h2><p className="task-description">{tarefa.descricao || 'Esta tarefa ainda não tem uma descrição.'}</p></article>
      <aside className="surface detail-meta" aria-label="Informações da tarefa"><h2>Detalhes</h2><dl><div><dt>Prioridade</dt><dd><span className={`priority priority--${tarefa.prioridade}`}>{prioridades[tarefa.prioridade]}</span></dd></div><div><dt>Prazo</dt><dd>{formatDate(tarefa.prazo)}</dd></div><div><dt>Equipe</dt><dd>{tarefa.equipe}</dd></div><div><dt>Visibilidade</dt><dd>{tarefa.publica ? 'Pública' : 'Restrita à equipe'}</dd></div><div><dt>Código</dt><dd>#{tarefa.id}</dd></div></dl></aside>
    </div>
    <div className="detail-actions"><div>{editar && <Link className="button button--primary" to={`/tarefas/${tarefa.id}/editar${queryRetorno(voltar)}`}>Editar tarefa</Link>}<button className="button button--ghost" onClick={() => { setErro(''); recarregar(); }}>Recarregar dados</button></div>{admin && <button className="button button--danger" onClick={() => setConfirmar(true)}>Excluir tarefa</button>}</div>
    {!editar && <p className="permission-note">Você pode consultar esta tarefa. A edição está disponível para o responsável e para quem tem acesso à equipe.</p>}
    <dialog className="confirm-dialog" ref={dialog} onCancel={event => { if (excluindo) event.preventDefault(); else setConfirmar(false); }} onClose={() => setConfirmar(false)} aria-labelledby="delete-title" aria-describedby="delete-description"><div className="dialog-icon" aria-hidden="true">!</div><h2 id="delete-title">Excluir esta tarefa?</h2><p id="delete-description">“{tarefa.titulo}” será excluída permanentemente. Esta ação não pode ser desfeita.</p><div className="dialog-actions"><button autoFocus className="button button--ghost" disabled={excluindo} onClick={() => setConfirmar(false)}>Cancelar</button><button className="button button--danger" disabled={excluindo} onClick={excluir}>{excluindo ? 'Excluindo…' : 'Sim, excluir tarefa'}</button></div></dialog>
  </>;
}
