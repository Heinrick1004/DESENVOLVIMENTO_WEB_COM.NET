import { parseArgs } from 'node:util';
import createClient from 'openapi-fetch';
import type { paths } from '../src/api/schema';

const { values } = parseArgs({ options: { 'api-url': { type: 'string' } } });
const address = new URL(values['api-url'] || process.env.API_URL || 'https://localhost:7240');
if (address.protocol !== 'https:' || address.username || address.password) {
  throw new Error('Informe uma URL HTTPS sem credenciais para a verificacao autenticada.');
}

// Fetch no Node não armazena cookies. Este jar existe apenas durante a execução,
// permanece restrito à origem HTTPS escolhida e nunca é gravado ou impresso.
const cookies = new Map<string, string>();
let csrf: string | undefined;
async function sessionFetch(request: Request): Promise<Response> {
  if (new URL(request.url).origin !== address.origin) {
    throw new Error('A requisicao deve permanecer na origem HTTPS configurada.');
  }
  const headers = new Headers(request.headers);
  if (cookies.size) headers.set('Cookie', [...cookies].map(([name, value]) => `${name}=${value}`).join('; '));
  if (!['GET', 'HEAD', 'OPTIONS'].includes(request.method.toUpperCase())) {
    if (!csrf) throw new Error('Token CSRF ausente para a operacao.');
    headers.set('X-CSRF-TOKEN', csrf);
  }
  const response = await fetch(new Request(request, {
    headers, redirect: 'error', signal: AbortSignal.timeout(15_000),
  }));
  for (const cookie of response.headers.getSetCookie()) {
    const assignment = cookie.split(';')[0] || '';
    const separator = assignment.indexOf('=');
    if (separator <= 0) continue;
    const name = assignment.slice(0, separator).trim();
    const value = assignment.slice(separator + 1).trim();
    if (value) cookies.set(name, value);
    else cookies.delete(name);
  }
  return response;
}

const client = createClient<paths>({ baseUrl: address.origin, fetch: sessionFetch });
function body<T>(data: T | undefined, response: Response, expected: number, operation: string): T {
  if (response.status !== expected || data === undefined) {
    throw new Error(`${operation}: resposta HTTP inesperada (${response.status}).`);
  }
  return data;
}
function report(chamada: string, status: number, data: { id?: number; temRowVersion?: boolean } = {}) {
  console.log(JSON.stringify({ chamada, status, ...data }));
}
async function refreshCsrf() {
  const result = await client.GET('/api/auth/csrf');
  csrf = body(result.data, result.response, 200, 'GET /api/auth/csrf').token;
  if (!csrf) throw new Error('O servidor nao retornou o token CSRF.');
  report('GET /api/auth/csrf', result.response.status);
}

async function verify() {
  const email = process.env.PORTAL_SMOKE_EMAIL;
  const senha = process.env.PORTAL_SMOKE_PASSWORD;
  if (!email || !senha) {
    throw new Error('Configure PORTAL_SMOKE_EMAIL e PORTAL_SMOKE_PASSWORD no ambiente do processo.');
  }
  await refreshCsrf();
  const login = await client.POST('/api/auth/login', { body: { email, senha } });
  const session = body(login.data, login.response, 200, 'POST /api/auth/login');
  report('POST /api/auth/login', login.response.status);
  try {
    // O token anônimo deixa de ser válido após o login; obter o token da sessão.
    await refreshCsrf();
    const created = await client.POST('/api/tarefas', { body: {
      titulo: `Verificacao autenticada ${new Date().toISOString()}`,
      descricao: 'Evidencia local de criacao, metrica de negocio e span customizado.',
      situacao: 'Pendente', prioridade: 2, prazo: null, equipe: session.equipe, publica: false,
    } });
    const task = body(created.data, created.response, 201, 'POST /api/tarefas');
    report('POST /api/tarefas', created.response.status, { id: task.id, temRowVersion: Boolean(task.rowVersion) });
    if (!task.rowVersion) throw new Error('A criacao nao retornou a versao da tarefa.');

    const result = await client.GET('/api/tarefas/{id}', { params: { path: { id: task.id } } });
    const detail = body(result.data, result.response, 200, 'GET /api/tarefas/{id}');
    report('GET /api/tarefas/{id}', result.response.status, { id: detail.id, temRowVersion: Boolean(detail.rowVersion) });
    if (detail.id !== task.id || !detail.rowVersion) {
      throw new Error('O detalhe nao corresponde a tarefa criada e a sua versao.');
    }
  }
  finally {
    const logout = await client.POST('/api/auth/logout');
    report('POST /api/auth/logout', logout.response.status);
    if (logout.response.status !== 204) throw new Error('O logout nao foi confirmado pelo servidor.');
    cookies.clear();
    csrf = undefined;
  }
}

verify().catch((error: unknown) => {
  // Não imprimir corpos de resposta, objetos Request, cookies ou credenciais.
  console.error(error instanceof Error ? error.message : 'A verificacao autenticada falhou.');
  process.exitCode = 1;
});
