import { useCallback, useEffect, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import { ApiError, describeError, isAbort } from '../api/http';
import type { DetalheTarefa, Sessao, Situacao, TarefaInput } from '../api/types';
import { podeEditar, useAuth } from '../auth/AuthContext';
import { useAsync } from '../components/AsyncState';
import { ErrorNotice, Feedback } from '../components/Feedback';
import { prioridades, situacoes } from '../components/TaskBadge';
import { listaRetorno, queryRetorno } from './navigation';

export function TaskEditor() {
  const { id } = useParams();
  const { sessao } = useAuth();
  if (!sessao) return null;
  return id ? <EditLoader id={id} sessao={sessao} /> : <TaskForm sessao={sessao} />;
}

function EditLoader({ id, sessao }: { id: string; sessao: Sessao }) {
  const load = useCallback((signal: AbortSignal) => Number.isSafeInteger(Number(id)) && Number(id) > 0 ? api.detalhes(Number(id), signal) : Promise.reject(new ApiError('http', 404)), [id]);
  const { state, recarregar } = useAsync(load);
  if (state.estado === 'idle' || state.estado === 'loading') return <Feedback tipo={state.estado} titulo="Preparando a edição…" />;
  if (state.estado === 'error') return <Feedback tipo="error" titulo="Não foi possível carregar a tarefa" mensagem={state.erro} tentar={recarregar} />;
  if (!podeEditar(sessao, state.data)) return <Feedback tipo="error" titulo="A edição não está disponível" mensagem="Você pode editar tarefas da sua equipe, criadas por você ou autorizadas pelo seu perfil." />;
  return <TaskForm key={state.data.rowVersion} tarefa={state.data} sessao={sessao} />;
}

function TaskForm({ tarefa, sessao }: { tarefa?: DetalheTarefa; sessao: Sessao }) {
  const [params] = useSearchParams();
  const voltar = listaRetorno(params);
  const cancelar = tarefa ? `/tarefas/${tarefa.id}${queryRetorno(voltar)}` : voltar;
  const navigate = useNavigate();
  const [titulo, setTitulo] = useState(tarefa?.titulo || '');
  const [descricao, setDescricao] = useState(tarefa?.descricao || '');
  const [prioridade, setPrioridade] = useState(tarefa?.prioridade || 2);
  const [situacao, setSituacao] = useState<Situacao>(tarefa?.situacao || 'Pendente');
  const [prazo, setPrazo] = useState(tarefa?.prazo?.slice(0, 10) || '');
  const [publica, setPublica] = useState(tarefa?.publica ?? false);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState('');
  const [campos, setCampos] = useState<Record<string, string[]>>({});
  const [conflito, setConflito] = useState(false);
  const mutation = useRef<AbortController | null>(null);
  useEffect(() => () => mutation.current?.abort(), []);

  async function salvar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (salvando) return;
    setCampos({});
    setErro('');
    setConflito(false);
    if (titulo.trim().length < 3) {
      setCampos({ titulo: ['Informe um título com pelo menos 3 caracteres.'] });
      setErro('Revise o título antes de salvar.');
      return;
    }
    const controller = new AbortController();
    mutation.current = controller;
    setSalvando(true);
    const body: TarefaInput = { titulo: titulo.trim(), descricao: descricao.trim() || null, situacao, prioridade, prazo: prazo || null, publica, equipe: tarefa?.equipe || sessao.equipe };
    try {
      const data = tarefa ? await api.editar(tarefa.id, { ...body, rowVersion: tarefa.rowVersion }, controller.signal) : await api.criar(body, controller.signal);
      if (!controller.signal.aborted) navigate(`/tarefas/${data.id}${queryRetorno(voltar)}`, { replace: true, state: { mensagem: tarefa ? 'Alterações salvas com sucesso.' : 'Tarefa criada com sucesso.' } });
    } catch (error) {
      if (isAbort(error) || controller.signal.aborted) return;
      setErro(describeError(error));
      if (error instanceof ApiError) {
        setConflito(error.status === 409);
        const fields: Record<string, string[]> = {};
        for (const [key, messages] of Object.entries(error.problem.errors || {})) fields[key.toLowerCase().split('.').at(-1) || key] = messages;
        setCampos(fields);
      }
    } finally { if (!controller.signal.aborted) setSalvando(false); }
  }

  const inputError = (campo: string) => campos[campo]?.length ? `${campo}-error` : undefined;
  const fieldError = (campo: string) => campos[campo]?.length ? <p id={`${campo}-error`} className="field-error">{campos[campo]?.join(' ')}</p> : null;

  return <>
    <Link className="back-link" to={cancelar}>← {tarefa ? 'Voltar aos detalhes' : 'Voltar às tarefas'}</Link>
    <div className="page-heading"><div><p className="eyebrow">{tarefa ? `TAREFA #${tarefa.id}` : 'COMECE POR UM PASSO'}</p><h1 tabIndex={-1}>{tarefa ? 'Editar tarefa' : 'Uma nova tarefa.'}</h1><p className="page-description">Defina o que precisa ser feito. O resto começa por aqui.</p></div></div>
    <form onSubmit={salvar} className="surface task-form" aria-busy={salvando}>
      <div className="form-intro"><h2>Informações da tarefa</h2><p>Os campos marcados com * são obrigatórios.</p></div>
      <ErrorNotice erro={erro} />
      {conflito && <p className="conflict-help">Suas alterações continuam no formulário. Copie o que precisar e <Link to={cancelar}>abra os detalhes atualizados</Link> antes de editar novamente.</p>}
      <div className="field"><label htmlFor="titulo">Título <span aria-hidden="true">*</span></label><input id="titulo" value={titulo} onChange={event => setTitulo(event.target.value)} required minLength={3} maxLength={120} placeholder="O que precisa ser feito?" aria-invalid={Boolean(campos.titulo)} aria-describedby={inputError('titulo')} />{fieldError('titulo')}<span className="field-hint">{titulo.length}/120 caracteres</span></div>
      <div className="field"><label htmlFor="descricao">Descrição</label><textarea id="descricao" value={descricao} onChange={event => setDescricao(event.target.value)} rows={5} maxLength={2000} placeholder="Adicione contexto, orientações ou os próximos passos…" aria-invalid={Boolean(campos.descricao)} aria-describedby={inputError('descricao')} />{fieldError('descricao')}<span className="field-hint">{descricao.length}/2000 caracteres</span></div>
      <div className="form-row"><div className="field"><label htmlFor="situacao">Situação <span aria-hidden="true">*</span></label><select id="situacao" value={situacao} onChange={event => setSituacao(event.target.value as Situacao)} required>{Object.entries(situacoes).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>{fieldError('situacao')}</div><div className="field"><label htmlFor="prioridade">Prioridade <span aria-hidden="true">*</span></label><select id="prioridade" value={prioridade} onChange={event => setPrioridade(Number(event.target.value))} required>{Object.entries(prioridades).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select>{fieldError('prioridade')}</div><div className="field"><label htmlFor="prazo">Prazo</label><input id="prazo" type="date" value={prazo} onChange={event => setPrazo(event.target.value)} aria-invalid={Boolean(campos.prazo)} aria-describedby={inputError('prazo')} />{fieldError('prazo')}</div></div>
      <div className="field"><label htmlFor="equipe">Equipe responsável</label><input id="equipe" value={tarefa?.equipe || sessao.equipe} readOnly aria-describedby="equipe-hint" /><span id="equipe-hint" className="field-hint">A tarefa permanece vinculada à equipe responsável.</span>{fieldError('equipe')}</div>
      <label className="checkbox-field"><input type="checkbox" checked={publica} onChange={event => setPublica(event.target.checked)} /><span><strong>Permitir consulta pública</strong><span>Pessoas sem sessão poderão consultar esta tarefa. A edição continua protegida.</span></span></label>
      <div className="form-actions"><Link className={`button button--ghost${salvando ? ' disabled-link' : ''}`} to={cancelar} aria-disabled={salvando} onClick={event => { if (salvando) event.preventDefault(); }}>Cancelar</Link><button className="button button--primary" type="submit" disabled={salvando}>{salvando ? 'Salvando…' : tarefa ? 'Salvar alterações' : 'Criar tarefa'}</button></div>
    </form>
  </>;
}
