import createClient from 'openapi-fetch';
import type { paths } from './schema';
import { apiFetch, baseUrl, refreshCsrf } from './http';
import type { Consulta, DetalheTarefa, PaginaTarefas, Sessao, TarefaInput } from './types';

// schema.d.ts é gerado do documento OpenAPI real. Caminhos, parâmetros e corpos
// abaixo são conferidos pelo TypeScript em cada geração e build.
export const client = createClient<paths>({ baseUrl, fetch: apiFetch, credentials: 'include' });

function required<T>(data: T | undefined): T {
  if (data === undefined) throw new Error('Resposta de sucesso sem corpo obrigatório.');
  return data;
}

export const api = {
  async listar(query: Consulta, signal?: AbortSignal): Promise<PaginaTarefas> {
    const { data } = await client.GET('/api/tarefas', { params: { query }, signal });
    return required(data);
  },
  async detalhes(id: number, signal?: AbortSignal): Promise<DetalheTarefa> {
    const { data } = await client.GET('/api/tarefas/{id}', { params: { path: { id } }, signal });
    return required(data);
  },
  async criar(body: TarefaInput, signal?: AbortSignal): Promise<DetalheTarefa> {
    const { data } = await client.POST('/api/tarefas', { body, signal });
    return required(data);
  },
  async editar(id: number, body: TarefaInput & { rowVersion: string }, signal?: AbortSignal): Promise<DetalheTarefa> {
    const { data } = await client.PUT('/api/tarefas/{id}', { params: { path: { id } }, body, signal });
    return required(data);
  },
  async excluir(id: number, rowVersion: string, signal?: AbortSignal): Promise<void> {
    await client.DELETE('/api/tarefas/{id}', { params: { path: { id }, query: { rowVersion } }, signal });
  },
  async sessao(signal?: AbortSignal): Promise<Sessao> {
    const { data } = await client.GET('/api/auth/me', { signal });
    return required(data);
  },
  async entrar(email: string, senha: string): Promise<Sessao> {
    const { data } = await client.POST('/api/auth/login', { body: { email, senha } });
    await refreshCsrf();
    return required(data);
  },
  async sair(): Promise<void> {
    await client.POST('/api/auth/logout');
    await refreshCsrf();
  },
};
