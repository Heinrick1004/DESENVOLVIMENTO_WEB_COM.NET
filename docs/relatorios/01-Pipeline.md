# Roteiro 1 — Pipeline HTTP, binding e Minimal APIs versus MVC

**Disciplina:** Desenvolvimento Web com .NET — UNIP. **Fonte:** Roteiros.pdf, páginas 3–5. **Consolidação:** 29/10/2026. Dados do aluno, RA, turma e professor devem ser preenchidos pelo responsável pela entrega; não foram presumidos.

## Objetivo e introdução

Construir a base executável do Portal de Tarefas e observar o percurso HTTP, utilizando quatro fontes de binding e dois endpoints diagnósticos equivalentes. O relatório descreve a solução cumulativa existente e corrigida, sem reconstruir artificialmente uma execução histórica anterior ao Identity ou ao EF Core.

## Ferramentas e estrutura

O projeto `PortalTarefas.Web` tem alvo `net10.0`, SDK selecionado em `global.json`, controllers MVC, Minimal APIs e configuração por ambiente. A versão efetivamente executada e os resultados de build/testes ficam no [status de execução](../STATUS_DE_EXECUCAO.md). O código completo exigido pelo roteiro está no [anexo 01](anexos/01-Codigo.md), que deve corresponder à versão final dos arquivos.

## Desenvolvimento e funcionalidades

`Program.cs` registra MVC, tratamento de erros, CORS nomeado `FrontendLocal`, documentação OpenAPI e os demais serviços cumulativos. `RequisicaoMiddleware` envolve a requisição, valida o identificador de correlação recebido, devolve `X-Correlation-ID` e registra método, caminho, status e duração sem copiar corpos, cookies, tokens ou senhas.

Ordem do pipeline da solução cumulativa:

1. Registro/correlação de requisição, que envolve a execução restante e registra a saída.
2. Tratamento centralizado de exceções e padronização de respostas sem corpo.
3. HSTS no ambiente apropriado e redirecionamento HTTPS.
4. Arquivos estáticos e seleção de rota.
5. CORS com origem explícita.
6. Autenticação e autorização, incorporadas pelo roteiro 6.
7. Limitação de requisições e output cache, incorporados pelo roteiro 7.
8. Endpoints de diagnóstico, API, MVC, saúde e SPA.

O filtro de ação observa a execução interna do controller. A entrada registrada pelo middleware antecede a ação; sua saída ocorre depois do retorno da ação e do tratamento aplicável. O arquivo final `Program.cs` é a referência para a ordem exata; os logs reais devem corroborá-la.

| Fonte de binding | Demonstração |
|---|---|
| Rota | `{id:int}` nos diagnósticos e na consulta de tarefa |
| Query string | `filtro`, `pagina`, `tamanho` e filtros de tarefa |
| Cabeçalho | `X-Cliente` no diagnóstico; `X-Correlation-ID` no middleware |
| JSON | Corpo de cadastro de tarefa em `POST /api/tarefas` |

`GET /api/minimal/diagnostico/{id}` e `GET /api/diagnostico/{id}` devolvem `DiagnosticoDto` com o mesmo conteúdo para os mesmos dados de rota/query/header. As operações de tarefas usam DTOs separados das entidades. Na versão cumulativa, cadastrar exige autenticação e CSRF; os controles posteriores foram preservados ao validar as funcionalidades iniciais.

## Decisões técnicas

| Eixo | Minimal API | Controller MVC/API |
|---|---|---|
| Organização | Delegate próximo ao mapeamento; adequado ao diagnóstico curto | Ações agrupadas por recurso e convenções claras |
| Dependências | Parâmetros do handler resolvidos pelo contêiner | Injeção no construtor e dependências compartilhadas entre ações |
| Validação | Requer configuração/comportamento explícito para o modelo | `ApiController`/ModelState e atributos integram a validação |
| Filtros | Filtros de endpoint têm ciclo próprio | Filtros MVC observam ações e resultados; Razor integra formulários |
| Testes | Integração HTTP exercita binding e pipeline | Integração HTTP e testes de ação/validação exercitam contratos e Razor |

Controllers são priorizados para o CRUD e a interface Razor porque compartilham validação, filtros, DTOs e organização das ações. Minimal APIs permanecem para diagnósticos, saúde e leitura pública curta. Essa escolha atende à combinação de API e MVC exigida nas próximas atividades.

## Testes, resultados e evidências

`ApiTests` e os testes básicos do pipeline verificam diagnósticos equivalentes, binding, correlação, redirecionamento e operações HTTP. A execução prioritária consolidada aprovou 4 testes dos roteiros 1 e 2, incluindo o View Component, com transcrições de requests/responses e logs em `docs/evidencias/roteiros-1-2.trx`. As execuções posteriores da suíte cumulativa e os smokes HTTPS estão consolidados em [STATUS_DE_EXECUCAO.md](../STATUS_DE_EXECUCAO.md); resultados não são inferidos da presença dos testes.

Para reproduzir, seguir o README, compilar a solução, iniciar HTTPS e executar as integrações em banco exclusivo quando o cenário usar SQL/Identity. Exemplos de requests ficam em [requisicoes.http](../requisicoes.http). Esse arquivo é procedimento; só uma resposta efetivamente coletada conta como evidência de execução.

## Aprimoramento e conclusão

O middleware é reutilizável por classe e extensão de `IApplicationBuilder`, com correlação devolvida ao cliente. Os testes de integração complementam o comportamento observável do pipeline. A implementação está identificada, mas a conclusão da atividade depende das verificações e evidências individuais registradas na matriz; pendências documentais ou de execução permanecem explícitas.
