export function listaRetorno(params: URLSearchParams): string {
  const value = params.get('voltar');
  return value === '/tarefas' || value?.startsWith('/tarefas?') ? value : '/tarefas';
}

export function queryRetorno(value: string): string {
  return `?voltar=${encodeURIComponent(value)}`;
}
