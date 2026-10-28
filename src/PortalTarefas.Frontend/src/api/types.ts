import type { components, paths } from './schema';

// Estes nomes são aliases de uso da interface. Campos, nulabilidade, corpos e
// respostas permanecem definidos apenas no schema gerado do servidor.
export type Tarefa = components['schemas']['TarefaResumoDto'];
export type DetalheTarefa = components['schemas']['TarefaDetalheDto'];
export type TarefaInput = components['schemas']['CriarTarefaDto'];
export type TarefaAtualizacao = components['schemas']['AtualizarTarefaDto'];
export type Sessao = components['schemas']['SessaoDto'];
export type PaginaTarefas = paths['/api/tarefas']['get']['responses'][200]['content']['application/json'];
export type Situacao = NonNullable<TarefaInput['situacao']>;

type ConsultaApi = NonNullable<paths['/api/tarefas']['get']['parameters']['query']>;
type CamposComPadrao = 'pagina' | 'tamanho' | 'ordenarPor' | 'direcao';
// A URL da lista sempre materializa os padrões de paginação e ordenação.
export type Consulta = Required<Pick<ConsultaApi, CamposComPadrao>> & Omit<ConsultaApi, CamposComPadrao>;
