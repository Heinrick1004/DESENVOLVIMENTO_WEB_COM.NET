import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api/client';
import { ApiError } from '../api/http';
import type { PaginaTarefas } from '../api/types';
import { AuthProvider } from '../auth/AuthContext';
import { TaskList } from './TaskList';

vi.mock('../api/client', () => ({ api: { sessao: vi.fn(), listar: vi.fn() } }));
const empty: PaginaTarefas = { itens: [], pagina: 1, tamanho: 10, total: 0, totalPaginas: 0 };
const success: PaginaTarefas = { ...empty, total: 1, totalPaginas: 1, itens: [{ id: 7, titulo: 'Preparar entrega acadêmica', descricao: 'Revisar', prioridade: 3, prazo: '2026-11-05', situacao: 'Pendente', equipe: 'Desenvolvimento', publica: true, proprietarioId: 'id' }] };
function Location() { const { search } = useLocation(); return <output data-testid="query">{search}</output>; }
function mount(path = '/tarefas') {
  return render(<MemoryRouter initialEntries={[path]}><AuthProvider><Routes><Route path="/tarefas" element={<><TaskList /><Location /></>} /></Routes></AuthProvider></MemoryRouter>);
}

describe('lista de tarefas e seus estados', () => {
  beforeEach(() => { vi.resetAllMocks(); vi.mocked(api.sessao).mockRejectedValue(new ApiError('http', 401)); });

  it('passa de loading para empty sem exibir resultados inexistentes', async () => {
    let resolve!: (value: PaginaTarefas) => void;
    vi.mocked(api.listar).mockImplementation(() => new Promise(done => { resolve = done; }));
    mount();
    expect(screen.getByText('Carregando tarefas…')).toBeInTheDocument();
    await act(async () => resolve(empty));
    expect(await screen.findByText('Um novo espaço para suas ideias')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('apresenta resultado real, situação, prioridade e prazo pt-BR', async () => {
    vi.mocked(api.listar).mockResolvedValue(success);
    mount();
    expect(await screen.findByRole('link', { name: 'Preparar entrega acadêmica' })).toHaveAttribute('href', '/tarefas/7?voltar=%2Ftarefas');
    expect(screen.getByText('05/11/2026')).toBeInTheDocument();
    expect(screen.getByText('Alta', { selector: 'span' })).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
  });

  it('expõe erro com retry e volta a consultar a API', async () => {
    vi.mocked(api.listar).mockRejectedValueOnce(new ApiError('network')).mockResolvedValueOnce(success);
    mount();
    expect(await screen.findByText('As tarefas não carregaram')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(await screen.findByRole('link', { name: 'Preparar entrega acadêmica' })).toBeInTheDocument();
    expect(api.listar).toHaveBeenCalledTimes(2);
  });

  it('restaura filtros pela URL e retorna à primeira página ao alterar situação', async () => {
    vi.mocked(api.listar).mockResolvedValue(success);
    mount('/tarefas?pagina=3&tamanho=20&busca=entrega&ordenarPor=titulo&direcao=desc');
    await screen.findByRole('table');
    expect(api.listar).toHaveBeenCalledWith(expect.objectContaining({ pagina: 3, tamanho: 20, busca: 'entrega', ordenarPor: 'titulo', direcao: 'desc' }), expect.any(AbortSignal));
    await userEvent.click(screen.getByRole('button', { name: 'Em andamento' }));
    await waitFor(() => expect(screen.getByTestId('query').textContent).toContain('situacao=EmAndamento'));
    expect(screen.getByTestId('query').textContent).toContain('pagina=1');
    expect(screen.getByRole('searchbox')).toHaveValue('entrega');
  });

  it('aborta a consulta anterior ao mudar filtros e não substitui a lista por resposta antiga', async () => {
    let oldSignal: AbortSignal | undefined;
    let resolveOld!: (value: PaginaTarefas) => void;
    vi.mocked(api.listar).mockImplementationOnce((_query, signal) => { oldSignal = signal; return new Promise(resolve => { resolveOld = resolve; }); }).mockResolvedValue(success);
    mount();
    await userEvent.click(screen.getByRole('button', { name: 'Em andamento' }));
    await screen.findByRole('table');
    expect(oldSignal?.aborted).toBe(true);
    await act(async () => resolveOld(empty));
    expect(screen.getByRole('table')).toBeInTheDocument();
    expect(screen.queryByText('Nenhuma tarefa com esses filtros')).not.toBeInTheDocument();
  });
});
