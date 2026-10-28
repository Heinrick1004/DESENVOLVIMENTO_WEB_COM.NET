import { describe, expect, it } from 'vitest';
import { gravarConsulta, lerConsulta } from './query';
import { listaRetorno } from './navigation';

describe('estado da lista na URL', () => {
  it('restaura busca, filtros, ordem e paginação de um link compartilhável', () => {
    const query = lerConsulta(new URLSearchParams('pagina=3&tamanho=20&ordenarPor=titulo&direcao=desc&busca=entrega&situacao=EmAndamento&prioridade=3'));
    expect(query).toEqual({ pagina: 3, tamanho: 20, ordenarPor: 'titulo', direcao: 'desc', busca: 'entrega', situacao: 'EmAndamento', prioridade: 3 });
    expect(lerConsulta(gravarConsulta(query))).toEqual(query);
  });
  it('normaliza valores inválidos sem enviá-los à API', () => {
    expect(lerConsulta(new URLSearchParams('pagina=-1&tamanho=999999&ordenarPor=sql&direcao=x&situacao=inválida&prioridade=8'))).toEqual({ pagina: 1, tamanho: 10, ordenarPor: 'prazo', direcao: 'asc', busca: undefined, situacao: undefined, prioridade: undefined });
  });
  it('não aceita redirecionamento externo no retorno da lista', () => {
    expect(listaRetorno(new URLSearchParams({ voltar: 'https://externo.test' }))).toBe('/tarefas');
    expect(listaRetorno(new URLSearchParams({ voltar: '/tarefas?pagina=2' }))).toBe('/tarefas?pagina=2');
  });
});
