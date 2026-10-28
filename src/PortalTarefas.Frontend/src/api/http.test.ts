import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiFetch, ApiError, refreshCsrf, resetCsrf } from './http';

describe('transporte HTTP e proteção da sessão', () => {
  beforeEach(() => resetCsrf());

  it('envia cookie em leituras e não busca CSRF para GET', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}'));
    vi.stubGlobal('fetch', fetchMock);
    await apiFetch(new Request('http://localhost/api/tarefas'));
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({ credentials: 'include' });
    expect((fetchMock.mock.calls[0]?.[0] as Request).headers.has('X-CSRF-TOKEN')).toBe(false);
  });

  it('busca token e o envia em mutações, inclusive login; reutiliza até renovar', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(Response.json({ token: 'antes-login' }))
      .mockResolvedValueOnce(Response.json({ id: 'usuario' }))
      .mockResolvedValueOnce(Response.json({ token: 'apos-login' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await apiFetch(new Request('http://localhost/api/auth/login', { method: 'POST', body: '{}' }));
    expect((fetchMock.mock.calls[1]?.[0] as Request).headers.get('X-CSRF-TOKEN')).toBe('antes-login');
    await refreshCsrf();
    await apiFetch(new Request('http://localhost/api/tarefas/1', { method: 'DELETE' }));
    expect((fetchMock.mock.calls[3]?.[0] as Request).headers.get('X-CSRF-TOKEN')).toBe('apos-login');
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  it('compartilha a busca de CSRF entre mutações simultâneas', async () => {
    const fetchMock = vi.fn().mockImplementation(async (input: Request | string) => String(input instanceof Request ? input.url : input).endsWith('/csrf') ? Response.json({ token: 'compartilhado' }) : new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await Promise.all([apiFetch(new Request('http://localhost/api/a', { method: 'POST' })), apiFetch(new Request('http://localhost/api/b', { method: 'POST' }))]);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it.each([400, 401, 403, 404, 409, 500])('preserva status HTTP %i e ProblemDetails', async status => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ title: 'Falha', detail: 'Detalhe verificável', errors: { Titulo: ['Obrigatório'] }, traceId: 'trace-1' }, { status })));
    await expect(apiFetch(new Request('http://localhost/api/tarefas'))).rejects.toMatchObject({ kind: 'http', status, message: 'Detalhe verificável', problem: { traceId: 'trace-1', errors: { Titulo: ['Obrigatório'] } } });
  });

  it('distingue erro de rede de uma resposta HTTP', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    await expect(apiFetch(new Request('http://localhost/api/tarefas'))).rejects.toMatchObject({ kind: 'network', status: 0 });
  });

  it('preserva AbortError para descartar uma requisição cancelada', async () => {
    const error = new DOMException('Aborted', 'AbortError');
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(error));
    await expect(apiFetch(new Request('http://localhost/api/tarefas'))).rejects.toBe(error);
  });

  it('não exibe HTML do proxy como mensagem ao usuário', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('<html>erro interno</html>', { status: 502 })));
    await expect(apiFetch(new Request('http://localhost/api/tarefas'))).rejects.toBeInstanceOf(ApiError);
  });

  it('informa expiração de sessão ao receber 401', async () => {
    const listener = vi.fn();
    window.addEventListener('session-expired', listener);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
    await expect(apiFetch(new Request('http://localhost/api/auth/me'))).rejects.toMatchObject({ status: 401 });
    expect(listener).toHaveBeenCalledTimes(1);
    window.removeEventListener('session-expired', listener);
  });
});
