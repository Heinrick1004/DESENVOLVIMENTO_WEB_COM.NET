import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api/client';
import { ApiError } from '../api/http';
import { AuthProvider, podeEditar, ProtectedRoute, retornoSeguro } from './AuthContext';
import type { DetalheTarefa, Sessao } from '../api/types';

vi.mock('../api/client', () => ({ api: { sessao: vi.fn(), entrar: vi.fn(), sair: vi.fn() } }));
const sessao: Sessao = { id: 'eu', email: 'pessoa@exemplo.test', equipe: 'Equipe A', roles: ['Usuario'], podeEditarOutrasEquipes: false };

function Location() { const location = useLocation(); return <p data-testid="location">{location.pathname}{location.search}</p>; }

describe('sessão e proteção de rotas', () => {
  beforeEach(() => vi.resetAllMocks());
  it('restaura sessão antes de renderizar o conteúdo protegido', async () => {
    vi.mocked(api.sessao).mockResolvedValue(sessao);
    render(<MemoryRouter initialEntries={['/tarefas/nova']}><AuthProvider><Routes><Route path="*" element={<ProtectedRoute><p>Formulário protegido</p></ProtectedRoute>} /></Routes></AuthProvider></MemoryRouter>);
    expect(screen.getByText('Restaurando sua sessão…')).toBeInTheDocument();
    expect(await screen.findByText('Formulário protegido')).toBeInTheDocument();
    expect(api.sessao).toHaveBeenCalledWith(expect.any(AbortSignal));
  });
  it('preserva a rota e a query para retornar após login', async () => {
    vi.mocked(api.sessao).mockRejectedValue(new ApiError('http', 401));
    render(<MemoryRouter initialEntries={['/tarefas/12/editar?voltar=%2Ftarefas%3Fpagina%3D2']}><AuthProvider><Routes><Route path="/entrar" element={<Location />} /><Route path="*" element={<ProtectedRoute><p>Protegido</p></ProtectedRoute>} /></Routes></AuthProvider></MemoryRouter>);
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/entrar?retorno='));
    expect(decodeURIComponent(screen.getByTestId('location').textContent || '')).toContain('/tarefas/12/editar?voltar=%2Ftarefas%3Fpagina%3D2');
  });
  it('não converte indisponibilidade do servidor em logout silencioso', async () => {
    vi.mocked(api.sessao).mockRejectedValue(new ApiError('network'));
    render(<MemoryRouter><AuthProvider><ProtectedRoute><p>Protegido</p></ProtectedRoute></AuthProvider></MemoryRouter>);
    expect(await screen.findByText('Não foi possível verificar sua sessão')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tentar novamente' })).toBeInTheDocument();
  });
  it.each(['https://externo.test', '//externo.test', '/\\externo.test', '/entrar'])('rejeita retorno inseguro %s', value => expect(retornoSeguro(value)).toBe('/tarefas'));
  it('permite edição pelo proprietário, equipe ou claim', () => {
    const tarefa = { proprietarioId: 'outra', equipe: 'Outra' } as DetalheTarefa;
    expect(podeEditar(sessao, tarefa)).toBe(false);
    expect(podeEditar(sessao, { ...tarefa, proprietarioId: 'eu' })).toBe(true);
    expect(podeEditar(sessao, { ...tarefa, equipe: 'Equipe A' })).toBe(true);
    expect(podeEditar({ ...sessao, podeEditarOutrasEquipes: true }, tarefa)).toBe(true);
  });
});
