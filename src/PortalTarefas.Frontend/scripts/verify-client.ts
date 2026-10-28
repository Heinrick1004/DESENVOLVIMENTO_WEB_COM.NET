import { parseArgs } from 'node:util';
import createClient from 'openapi-fetch';
import type { paths } from '../src/api/schema';

const { values } = parseArgs({ options: { 'api-url': { type: 'string' } } });
const address = new URL(values['api-url'] || process.env.API_URL || 'http://127.0.0.1:5240');
if (!['http:', 'https:'].includes(address.protocol) || address.username || address.password) {
  throw new Error('Informe API_URL ou --api-url com uma URL HTTP(S), sem credenciais.');
}

// Usa os caminhos, parâmetros e respostas gerados do OpenAPI real. Este arquivo
// também passa pelo tsc; a remoção de tipos do Node não substitui essa verificação.
const client = createClient<paths>({ baseUrl: address.origin });

async function verify() {
  const { data, error, response } = await client.GET('/api/tarefas', {
    params: { query: { pagina: 1, tamanho: 5, ordenarPor: 'id', direcao: 'asc' } },
  });
  if (!response.ok || error || !data || !Array.isArray(data.itens)) {
    throw new Error(`A listagem pelo cliente gerado falhou (HTTP ${response.status}).`);
  }
  console.log(JSON.stringify({ chamada: 'GET /api/tarefas', status: response.status,
    pagina: data.pagina, tamanho: data.tamanho, total: data.total, itens: data.itens.length }));

  const first = data.itens[0];
  if (first) {
    const detail = await client.GET('/api/tarefas/{id}', { params: { path: { id: first.id } } });
    if (!detail.response.ok || detail.error || !detail.data || detail.data.id !== first.id) {
      throw new Error(`A consulta de detalhes pelo cliente gerado falhou (HTTP ${detail.response.status}).`);
    }
    console.log(JSON.stringify({ chamada: 'GET /api/tarefas/{id}', status: detail.response.status, id: detail.data.id }));
  }
}

verify().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : 'A verificação do cliente falhou.');
  process.exitCode = 1;
});
