// Contrato provisório durante a construção da API; será vinculado ao schema real gerado.
export type Situacao = 'Pendente' | 'EmAndamento' | 'Concluida';
export interface Tarefa {
  id: number;
  titulo: string;
  descricao: string | null;
  situacao: Situacao;
  prioridade: number;
  prazo: string | null;
  equipe: string;
  publica: boolean;
  proprietarioId: string;
}
export interface DetalheTarefa extends Tarefa { rowVersion: string; criadaEm: string }
export interface TarefaInput {
  titulo: string;
  descricao: string | null;
  situacao: Situacao;
  prioridade: number;
  prazo: string | null;
  equipe: string;
  publica: boolean;
}
export interface Sessao { id: string; email: string; equipe: string; roles: string[]; podeEditarOutrasEquipes: boolean }
export interface PaginaTarefas { itens: Tarefa[]; pagina: number; tamanho: number; total: number; totalPaginas: number }
export interface Consulta {
  pagina: number;
  tamanho: number;
  ordenarPor: 'prazo' | 'titulo' | 'prioridade' | 'criadaEm' | 'id';
  direcao: 'asc' | 'desc';
  busca?: string;
  situacao?: Situacao;
  prioridade?: number;
}
