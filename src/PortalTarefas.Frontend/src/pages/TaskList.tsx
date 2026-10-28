import { useCallback, useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Consulta, PaginaTarefas } from '../api/types';
import { useAsync } from '../components/AsyncState';
import { Feedback } from '../components/Feedback';
import { formatDate, prioridades, situacoes, TaskBadge } from '../components/TaskBadge';
import { gravarConsulta, lerConsulta } from './query';

const vazia = (value: PaginaTarefas) => value.itens.length === 0;

export function TaskList() {
  const [params, setParams] = useSearchParams();
  const key = params.toString();
  const query = useMemo(() => lerConsulta(new URLSearchParams(key)), [key]);
  const [busca, setBusca] = useState(query.busca || '');
  const location = useLocation();
  const { sessao } = useAuth();
  const sessaoId = sessao?.id;
  const load = useCallback((signal: AbortSignal) => api.listar(query, signal), [query, sessaoId]);
  const { state, recarregar } = useAsync(load, vazia);
  const loaded = state.estado === 'success' || state.estado === 'empty' ? state.data : null;
  const voltar = encodeURIComponent(`/tarefas${location.search}`);
  const notice = (location.state as { mensagem?: string } | null)?.mensagem;

  useEffect(() => { setBusca(query.busca || ''); }, [query.busca]);

  function atualizar(value: Partial<Consulta>) {
    setParams(gravarConsulta({ ...query, pagina: 1, ...value }));
  }

  function pesquisar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    atualizar({ busca: busca.trim() || undefined });
  }

  const filtrando = Boolean(query.busca || query.situacao || query.prioridade);

  return <>
    <div className="page-heading">
      <div><p className="eyebrow">SEU ESPAÇO DE TRABALHO</p><h1 tabIndex={-1}>Cada tarefa, um próximo passo.</h1><p className="page-description">Organize as prioridades e acompanhe as entregas da sua equipe.</p></div>
      <Link className="button button--primary" to={`/tarefas/nova?voltar=${voltar}`}><span aria-hidden="true">＋</span> Nova tarefa</Link>
    </div>
    {notice && <div className="notice notice--success" role="status">{notice}</div>}
    <section className="task-board" aria-label="Tarefas">
      <div className="board-heading"><div><span className="section-marker" aria-hidden="true" /><h2>Tarefas da equipe</h2>{loaded && <span className="count" aria-label={`${loaded.total} tarefas no resultado`}>{loaded.total}</span>}</div><span className="board-caption">Planejar. Fazer. Concluir.</span></div>
      <div className="filters">
        <form className="search" role="search" onSubmit={pesquisar}>
          <label className="sr-only" htmlFor="busca">Buscar tarefas</label><span aria-hidden="true">⌕</span>
          <input id="busca" type="search" placeholder="Buscar por título ou descrição" maxLength={120} value={busca} onChange={event => setBusca(event.target.value)} />
          <button type="submit">Buscar</button>
        </form>
        <label className="filter-label">Prioridade<select aria-label="Filtrar prioridade" value={query.prioridade || ''} onChange={event => atualizar({ prioridade: Number(event.target.value) || undefined })}><option value="">Todas</option>{Object.entries(prioridades).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <label className="filter-label">Ordenar por<select aria-label="Ordenar tarefas" value={`${query.ordenarPor}:${query.direcao}`} onChange={event => { const [ordenarPor, direcao] = event.target.value.split(':'); atualizar({ ordenarPor: ordenarPor as Consulta['ordenarPor'], direcao: direcao as Consulta['direcao'] }); }}>
          <option value="prazo:asc">Prazo mais próximo</option><option value="prazo:desc">Prazo mais distante</option><option value="prioridade:desc">Maior prioridade</option><option value="prioridade:asc">Menor prioridade</option><option value="titulo:asc">Título: A–Z</option><option value="titulo:desc">Título: Z–A</option><option value="situacao:asc">Situação: A–Z</option><option value="situacao:desc">Situação: Z–A</option><option value="criadaem:desc">Mais recentes</option><option value="criadaem:asc">Mais antigas</option><option value="id:asc">Código crescente</option><option value="id:desc">Código decrescente</option>
        </select></label>
      </div>
      <div className="status-filters" role="group" aria-label="Filtrar situação">
        <button aria-pressed={!query.situacao} className={!query.situacao ? 'active' : ''} onClick={() => atualizar({ situacao: undefined })}>Todas as tarefas</button>
        {Object.entries(situacoes).map(([value, label]) => <button key={value} aria-pressed={query.situacao === value} className={query.situacao === value ? 'active' : ''} onClick={() => atualizar({ situacao: value as Consulta['situacao'] })}>{label}</button>)}
        {filtrando && <button className="clear-filter" onClick={() => { setBusca(''); setParams(gravarConsulta({ ...lerConsulta(new URLSearchParams()), ordenarPor: query.ordenarPor, direcao: query.direcao })); }}>Limpar filtros</button>}
      </div>
      {(state.estado === 'idle' || state.estado === 'loading') && <Feedback tipo={state.estado} titulo={state.estado === 'idle' ? 'Preparando suas tarefas…' : 'Carregando tarefas…'} />}
      {state.estado === 'error' && <Feedback tipo="error" titulo="As tarefas não carregaram" mensagem={state.erro} tentar={recarregar} />}
      {state.estado === 'empty' && <Feedback tipo="empty" titulo={filtrando ? 'Nenhuma tarefa com esses filtros' : 'Um novo espaço para suas ideias'} mensagem={filtrando ? 'Experimente outro termo ou limpe os filtros para ver mais tarefas.' : 'Crie sua primeira tarefa para começar a organizar as entregas.'} />}
      {state.estado === 'success' && <div className="table-scroll" data-state="success"><table className="tasks-table"><caption className="sr-only">Resultado das tarefas filtradas</caption><thead><tr><th scope="col">Tarefa</th><th scope="col">Prioridade</th><th scope="col">Equipe</th><th scope="col">Prazo</th><th scope="col">Situação</th><th scope="col"><span className="sr-only">Acessar</span></th></tr></thead><tbody>{state.data.itens.map(tarefa => <tr key={tarefa.id}>
        <td className="task-title-cell"><div className={`task-icon task-icon--${tarefa.situacao}`} aria-hidden="true">{tarefa.situacao === 'Concluida' ? '✓' : tarefa.situacao === 'EmAndamento' ? '◒' : '○'}</div><div><Link className="task-title" to={`/tarefas/${tarefa.id}?voltar=${voltar}`}>{tarefa.titulo}</Link><span className="task-subtitle">#{String(tarefa.id).padStart(3, '0')} · {tarefa.publica ? 'Compartilhada publicamente' : 'Acesso da equipe'}</span></div></td>
        <td data-label="Prioridade"><span className={`priority priority--${tarefa.prioridade}`}><span aria-hidden="true">{tarefa.prioridade === 3 ? '↑' : tarefa.prioridade === 1 ? '↓' : '−'}</span>{prioridades[tarefa.prioridade] || 'Indefinida'}</span></td>
        <td data-label="Equipe"><span className="team-label">{tarefa.equipe}</span></td><td data-label="Prazo">{formatDate(tarefa.prazo)}</td><td data-label="Situação"><TaskBadge situacao={tarefa.situacao} /></td>
        <td className="table-action"><Link aria-label={`Ver detalhes: ${tarefa.titulo}`} to={`/tarefas/${tarefa.id}?voltar=${voltar}`}>↗</Link></td>
      </tr>)}</tbody></table></div>}
      {loaded && <div className="pagination"><p role="status" aria-live="polite">{loaded.total === 0 ? 'Nenhuma tarefa' : `${(loaded.pagina - 1) * loaded.tamanho + 1}–${Math.min(loaded.pagina * loaded.tamanho, loaded.total)} de ${loaded.total} tarefas`}</p>
        <label>Por página<select value={query.tamanho} onChange={event => atualizar({ tamanho: Number(event.target.value) })}>{[5, 10, 20, 50].map(value => <option key={value}>{value}</option>)}</select></label>
        <nav aria-label="Paginação"><button aria-label="Página anterior" disabled={query.pagina <= 1} onClick={() => atualizar({ pagina: query.pagina - 1 })}>←</button><span>Página {loaded.pagina} de {Math.max(1, loaded.totalPaginas)}</span><button aria-label="Próxima página" disabled={query.pagina >= loaded.totalPaginas} onClick={() => atualizar({ pagina: query.pagina + 1 })}>→</button></nav>
      </div>}
    </section>
    <div className="workspace-note"><span aria-hidden="true">✧</span><p>Pequenos passos, grandes entregas.<br /><span>Uma tarefa de cada vez.</span></p></div>
  </>;
}
