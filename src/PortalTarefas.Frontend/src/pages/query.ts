import type { Consulta, Situacao } from '../api/types';

export function lerConsulta(params: URLSearchParams): Consulta {
  const pagina = Number(params.get('pagina'));
  const tamanho = Number(params.get('tamanho'));
  const ordem = params.get('ordenarPor');
  const situacao = params.get('situacao');
  const prioridade = Number(params.get('prioridade'));
  return {
    pagina: Number.isSafeInteger(pagina) && pagina > 0 && pagina <= 100000 ? pagina : 1,
    tamanho: [5, 10, 20, 50].includes(tamanho) ? tamanho : 10,
    ordenarPor: ['prazo', 'titulo', 'prioridade', 'criadaEm', 'id'].includes(ordem || '') ? ordem as Consulta['ordenarPor'] : 'prazo',
    direcao: params.get('direcao') === 'desc' ? 'desc' : 'asc',
    busca: params.get('busca')?.trim().slice(0, 120) || undefined,
    situacao: ['Pendente', 'EmAndamento', 'Concluida'].includes(situacao || '') ? situacao as Situacao : undefined,
    prioridade: [1, 2, 3].includes(prioridade) ? prioridade : undefined,
  };
}

export function gravarConsulta(query: Consulta): URLSearchParams {
  const result = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') result.set(key, String(value));
  }
  return result;
}
