# Matriz de requisitos — Portal de Tarefas

Fonte primária: `../Roteiros.pdf`, 26 páginas, lidas integralmente em 28/10/2026; complementada pelo pedido anexado. O arquivo original está na pasta acima do workspace e foi preservado. Os itens abaixo serão atualizados com arquivos e resultados reais. Implementação não equivale a execução comprovada.

| Roteiro / páginas | Requisitos e evidências obrigatórias | Arquivos previstos | Verificação |
|---|---|---|---|
| 1 / 3–5 | net10; HTTPS; exceções; estáticos; CORS nomeado; logs entrada/saída; binding rota/query/header/JSON; 200/201/400/404; Minimal/MVC equivalentes; comparação técnica | Program.cs; Middleware; DiagnosticoController; DTOs; docs/requisicoes.http; relatório 01 | Build + integração HTTP + logs; pendente |
| 2 / 6–8 | MVC cumulativo; serviço memória por interface; InputModel; DataAnnotations PT; validação cruzada; ModelState; preservação; Index/Details/Create/Edit; Razor tipado; TagHelpers; layout/partial; filtro com duração/resultado | Controllers/TarefasController; Views; Filters; Models/DTOs; Services/InMemoryTarefaService; relatório 02 | Testes de validação/controller e HTTP Razor; pendente |
| 3 / 9–11 | EF10 SQL Server; contexto/pooling revisado; segredo externo; mapeamento; migration/SQL; seed idempotente; async; atualização+auditoria atômica; rowversion/conflito 2 sessões; projeção no-tracking e ADO parametrizado equivalentes | Data; Models; Services/EfTarefaService; Migrations; docs/sql; relatório 03 | Banco SQL Server isolado; migrations/seed/transação/conflito/painel; pendente |
| 4 / 12–14 | DTOs distintos; contrato HTTP/Location; filtros/ordem allowlist/paginação no banco limite50; ProblemDetails;409; AddOpenApi/MapOpenApi; UI só Development; schema salvo; geração TS e chamada real | Controllers/TarefasApiController; DTOs; docs/openapi.json; frontend/src/api; relatório 04 | Contrato + API + cliente regenerado; pendente |
| 5 / 15–17 | Node LTS/npm; Vite React TS; API por env/proxy; HTTP central; tipos; lista/detalhe/cadastro; estados idle/loading/empty/success/error; URL sincronizada;404; erros por campo; atraso/falha; cancelamento; build; acessibilidade | src/PortalTarefas.Frontend; relatório 05 | npm ci/test/build + integração; pendente |
| 6 / 18–20 | Identity EF/migration; senha/bloqueio; roles/seed externo; login/logout; justificativa cookie/bearer/JWT; CSRF; claim outra equipe; leitura/criar/editar/excluir diferentes; servidor autoritativo; sessão/rotas React; returnUrl; matriz anônimo/usuário/admin 401/403/200 | Controllers/AuthController; Security; Data; frontend/Auth; relatório 06 | Integração cookie/CSRF/perfis/requisição direta; pendente |
| 7 / 21–23 | logs estruturados/correlação/escopos sem segredo; OTel ASP.NET/HTTP/SQL; métrica técnica/negócio/span/export; liveness/readiness; falha dependência; timeout/retry finito seguro/circuit breaker; rate limit429; cache público validade/invalidação; carga local | Infrastructure; Middleware; Program; tests; relatório 07 | Telemetria + saúde + resiliência + capacidade/cache; pendente |
| 8 / 24–26 | configuração ambiente/opções validadas/env sem segredo; Docker multistage/base fixada/nonroot; React integrado; porta/healthcheck; CI PR/main SDK explícito/.NET/npm testes/build; publicação após checks; imagem commit; staging/smoke/promoção/rollback; inventário dependências | Dockerfile; compose.yaml; .github/workflows; scripts; README; relatório 08 | Docker/CI/deploy dependem de ferramentas/destino; execução local pendente |

Extensões planejadas integradas: (1) middleware classe/extensão/correlação/testes; (2) ViewComponent/testes; (3) índice adicional/testes SQL isolados; (4) metadados paginação/contrato; (5) foco e anúncios/testes HTTP; (6) autorização por recurso/testes Identity; (7) teste de rate limiting; (8) inventário de dependências/SBOM.

## Histórico e limitações iniciais

- Pasta inicial continha apenas `Untitled-1.txt` vazio, preservado fora da solução; não havia repositório Git nem projeto anterior.
- SDK instalado: 10.0.403; runtime ASP.NET Core 10.0.14; Git 2.56.0.windows.2.
- Serviço MSSQLSERVER ativo; tentativa inicial `sqlcmd` falhou (SSL/credenciais no sandbox). Não houve alteração de banco existente.
- Node/npm e Docker ausentes do PATH inicial. Node portátil será tentado em diretório ignorado, sem instalação global.
- Os marcos Git terão somente resultados realmente observados; nenhuma execução histórica será inventada para simular oito entregas anteriores.
