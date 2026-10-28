import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api/client';
import { ApiError } from '../api/http';
import { AuthProvider, ProtectedRoute } from '../auth/AuthContext';
import { TaskEditor } from './TaskForm';

vi.mock('../api/client', () => ({ api: { sessao: vi.fn(), criar: vi.fn() } }));
function mount() {
  return render(<MemoryRouter initialEntries={['/tarefas/nova']}><AuthProvider><Routes><Route path="/tarefas/nova" element={<ProtectedRoute><TaskEditor /></ProtectedRoute>} /><Route path="/tarefas/:id" element={<p>Detalhes da tarefa criada</p>} /></Routes></AuthProvider></MemoryRouter>);
}

describe('formulário de tarefa', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(api.sessao).mockResolvedValue({ id: 'eu', email: 'eu@teste.test', equipe: 'Engenharia', roles: ['Usuario'], podeEditarOutrasEquipes: false });
  });
  it('envia formulário real com equipe da sessão e navega para o detalhe', async () => {
    vi.mocked(api.criar).mockResolvedValue({ id: 31, titulo: 'Preparar relatório', descricao: null, prioridade: 2, situacao: 'Pendente', prazo: null, equipe: 'Engenharia', publica: false, proprietarioId: 'eu', rowVersion: 'AAAA', criadaEm: '2026-10-28T12:00:00Z' });
    mount();
    await userEvent.type(await screen.findByLabelText(/Título/), 'Preparar relatório');
    await userEvent.click(screen.getByRole('button', { name: 'Criar tarefa' }));
    expect(await screen.findByText('Detalhes da tarefa criada')).toBeInTheDocument();
    expect(api.criar).toHaveBeenCalledWith({ titulo: 'Preparar relatório', descricao: null, prioridade: 2, situacao: 'Pendente', prazo: null, equipe: 'Engenharia', publica: false }, expect.any(AbortSignal));
  });
  it('bloqueia envios duplicados durante uma mutação', async () => {
    vi.mocked(api.criar).mockImplementation(() => new Promise(() => {}));
    mount();
    await userEvent.type(await screen.findByLabelText(/Título/), 'Criar um plano');
    await userEvent.dblClick(screen.getByRole('button', { name: 'Criar tarefa' }));
    expect(api.criar).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Salvando…' })).toBeDisabled();
  });
  it('preserva dados quando servidor retorna validação', async () => {
    vi.mocked(api.criar).mockRejectedValue(new ApiError('http', 400, { title: 'Revise os campos.', errors: { Titulo: ['Título já utilizado.'] } }));
    mount();
    const input = await screen.findByLabelText(/Título/);
    await userEvent.type(input, 'Não perder meu trabalho');
    await userEvent.click(screen.getByRole('button', { name: 'Criar tarefa' }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Revise os campos.'));
    expect(input).toHaveValue('Não perder meu trabalho');
    expect(screen.getByText('Título já utilizado.')).toBeInTheDocument();
    expect(input).toHaveAttribute('aria-invalid', 'true');
  });
});
