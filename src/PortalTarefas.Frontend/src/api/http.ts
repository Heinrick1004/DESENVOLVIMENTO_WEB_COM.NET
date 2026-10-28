export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly kind: 'http' | 'network';
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(kind: 'http' | 'network', status = 0, problem: ProblemDetails = {}) {
    const fallback = status === 401 ? 'Sua sessão expirou. Entre novamente.'
      : status === 403 ? 'Você não tem permissão para esta ação.'
      : status === 404 ? 'A tarefa não foi encontrada ou não está disponível.'
      : status === 409 ? 'Esta tarefa foi alterada por outra pessoa. Recarregue os dados antes de continuar.'
      : kind === 'network' ? 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.'
      : 'Não foi possível concluir a solicitação. Tente novamente.';
    super(problem.detail || problem.title || fallback);
    this.name = 'ApiError';
    this.kind = kind;
    this.status = status;
    this.problem = problem;
  }
}

const baseUrl = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || window.location.origin;
let csrf: string | undefined;
let csrfPending: Promise<string> | undefined;

export function resetCsrf() {
  csrf = undefined;
  csrfPending = undefined;
}

export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export function describeError(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Ocorreu um erro inesperado. Tente novamente.';
}

async function checkedFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  let response: Response;
  try {
    response = await fetch(input, { ...init, credentials: 'include' });
  } catch (error) {
    if (isAbort(error)) throw error;
    throw new ApiError('network');
  }
  if (!response.ok) {
    let problem: ProblemDetails = {};
    try { problem = await response.json() as ProblemDetails; } catch { /* Não ecoar páginas HTML de erro. */ }
    if (response.status === 401) {
      resetCsrf();
      window.dispatchEvent(new Event('session-expired'));
    }
    throw new ApiError('http', response.status, problem);
  }
  return response;
}

export async function refreshCsrf(): Promise<string> {
  resetCsrf();
  return getCsrf();
}

async function getCsrf(): Promise<string> {
  if (csrf) return csrf;
  csrfPending ??= checkedFetch(`${baseUrl}/api/auth/csrf`)
    .then(async response => {
      const body = await response.json() as { token: string };
      if (!body.token) throw new ApiError('http', 500, { detail: 'O servidor não retornou o token de segurança.' });
      csrf = body.token;
      return csrf;
    }).finally(() => { csrfPending = undefined; });
  return csrfPending;
}

/** Transporte compartilhado pelo cliente OpenAPI: cookie + CSRF + ProblemDetails. */
export async function apiFetch(input: Request): Promise<Response> {
  const headers = new Headers(input.headers);
  if (!['GET', 'HEAD', 'OPTIONS'].includes(input.method.toUpperCase())) {
    headers.set('X-CSRF-TOKEN', await getCsrf());
  }
  return checkedFetch(new Request(input, { headers, credentials: 'include' }));
}

export { baseUrl };
