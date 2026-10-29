# Estado de execução — Portal de Tarefas

Consolidação documental: 29/10/2026, America/Sao_Paulo. Fonte acadêmica: `Roteiros.pdf`, 26 páginas, relido durante a correção. Os registros preservam a data real de cada execução, inclusive as evidências de 28/10. O estado individual de cada exigência está na [matriz de requisitos](MATRIZ_DE_REQUISITOS.md); este arquivo consolida execuções, evidências e bloqueios.

## Critério de classificação

- **Concluído e verificado:** implementação e procedimento exigido possuem evidência real identificada.
- **Implementado sem execução:** há implementação inspecionada, mas falta a execução que comprova o requisito.
- **Reprovado:** o procedimento foi executado e falhou; registrar erro e nova execução após corrigir.
- **Bloqueado:** uma dependência ou permissão identificada impede a execução; registrar como resolver.
- **Pendente:** código, documento ou evidência exigida ainda precisa ser produzido.
- **Limitação histórica:** os marcos das oito entregas anteriores à auditoria não podem ser comprovados; a base e os novos marcos reais da correção são preservados, sem simular história.

Um teste ignorado é registrado como ignorado e não conta como aprovação. Testes de memória não comprovam comportamento específico de SQL Server, `rowversion`, rollback relacional ou Identity. A conclusão global de um roteiro exige todas as verificações obrigatórias; artefatos implementados e documentados não bastam.

## Situação por roteiro

| Roteiro | Implementação observada | Estado nesta revisão |
|---|---|---|
| 1 | Pipeline, middleware, binding, Minimal/MVC e contrato diagnóstico | Cenários automatizados e HTTPS real aprovados; provas identificadas na matriz |
| 2 | MVC, validação, filtro, Razor e View Component | Cenários automatizados e capturas MVC reais após correção de RowVersion aprovados |
| 3 | EF/SQL Server, migrations, seed, auditoria, concorrência e painel ADO | SQL real, migrations, seed repetido, versão/rollback e consultas equivalentes verificados |
| 4 | CRUD/contrato, OpenAPI e cliente TypeScript | Contrato real, tipos, CRUD, Swagger, exemplos e cliente HTTPS verificados |
| 5 | SPA, estados, URL, formulários e testes | 33 testes, typecheck, build e seis capturas reais dos cenários controlados aprovados |
| 6 | Identity/cookie/CSRF, roles/claim e proteção da SPA | Integração SQL/Identity e fluxo HTTPS real login/CSRF/criação/logout aprovados |
| 7 | Telemetria, saúde, resiliência, limitação e cache | Testes e exportação console real de span, métricas e logs correlacionados verificados |
| 8 | Configuração, Docker, CI e scripts de homologação | Implementado parcialmente; Docker/CI/CD exigem verificação própria |

## Resultados confirmados

| Verificação | Resultado observado | Evidência |
|---|---|---|
| Preservação da base Git | Commit `cad3fac`, tag `auditoria/base-existente-20261028`, branch `correcoes/auditoria-20261028` | `git log`, `git show-ref`; referência permanente no repositório |
| Marco da implementação corrigida | Commit `47c5f2fe37768d16c1a009b9b15688941e04bb02`; snapshots e relatórios não recriam marcos históricos anteriores | Histórico Git real da branch de correção |
| Compilação Release corrigida | Aprovada, zero avisos e zero erros | [build-dotnet.txt](evidencias/build-dotnet.txt) |
| Roteiros 1 e 2 prioritários | 4 aprovados, zero falhas; pipeline/binding/erro seguro, percurso MVC e View Component de vencidas | [roteiros-1-2.trx](evidencias/roteiros-1-2.trx), [comando](evidencias/roteiros-1-2-comando.txt) |
| Suíte .NET cumulativa com SQL real | 55 aprovados, zero falhos, zero ignorados; todas as suítes obrigatórias presentes | [integration.trx](evidencias/integracao/20261029T172845759Z-55df9f49/integration.trx), [summary.json](evidencias/integracao/20261029T172845759Z-55df9f49/summary.json), [comando](evidencias/integracao-final-comando.txt) |
| Banco exclusivo da integração | `PortalTarefas_Tests_20261028_dfc3ccbe`, criado para auditoria em 28/10 e reutilizado exclusivamente nos testes em 29/10; migrations/seed executados | Resumo e comando da integração acima |
| Geração do contrato | OpenAPI exportado de API real e `schema.d.ts` gerado por openapi-typescript | [frontend-geracao.txt](evidencias/frontend-geracao.txt), [openapi.json](openapi.json) |
| Typecheck frontend | Aprovado | [frontend-typecheck.txt](evidencias/frontend-typecheck.txt) |
| Testes frontend | 33 aprovados em 5 arquivos | [frontend-testes.txt](evidencias/frontend-testes.txt) |
| Build frontend | Aprovado, 54 módulos transformados | [frontend-build.txt](evidencias/frontend-build.txt) |
| Cliente gerado contra SQL real/HTTPS | Leitura da página e detalhes com 200; TLS validado pelo Node com a CA pública local | [frontend-cliente-sql.txt](evidencias/frontend-cliente-sql.txt) |
| Fluxo real autenticado do cliente gerado | CSRF 200, login 200, CSRF renovado 200, criação 201, detalhe 200 e logout 204 | [frontend-cliente-autenticado.txt](evidencias/frontend-cliente-autenticado.txt) |
| Seed SQL em instalação separada | Duas execuções: 3 tarefas, 3 auditorias, 2 roles e as mesmas 3 migrations; sem duplicação | [primeira](evidencias/sql-seed-primeira-execucao.txt), [segunda](evidencias/sql-seed-segunda-execucao.txt), [migrations](evidencias/sql-migrations-estado.txt) |
| Telemetria real exportada | Span `tarefa.criar`, counter de negócio origem API com valor 1, histograma técnico método/status e logs com trace/correlação | [telemetria-console.txt](evidencias/telemetria-console.txt) |
| Swagger UI real | Development, HTTP 200, 15 operações carregadas, sem interceptação | [imagem](evidencias/roteiro-04-openapi.png), [metadados](evidencias/roteiro-04-openapi.json) |
| Estados React e Problem Details | Seis capturas reais do React; HTTP, sessão, dados e erros simulados de forma explícita para os cenários | [manifesto](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/manifest.json) |
| Capturas MVC finais | Lista/detalhes/formulário inválido reais; mensagem em português e campos preservados; total de tarefas 15 antes/depois | [manifesto final](evidencias/roteiro-02-mvc/manifest.json), [histórico do bug](evidencias/roteiro-02-mvc/historico-rowversion-20261028/README.md) |
| Validação estática CI/scripts | YAML 27 etapas, sintaxe de 18 blocos Bash e 3 scripts PowerShell aprovados; ordem de gates conferida | [ci-validacao-estatica.json](evidencias/ci-validacao-estatica.json) |
| Inventários locais | Dependências .NET/npm geradas e preservadas | [.NET](evidencias/dependencias-dotnet.json), [npm](evidencias/dependencias-npm.json) |
| Revisão de arquivos/credenciais | 199 arquivos revisados, nenhum nome de arquivo sensível ou padrão conhecido de credencial encontrado; limites do método explícitos | [revisao-segredos.json](evidencias/revisao-segredos.json) |
| Development/Production simulada no mesmo artefato | Live, ready, MVC e SPA responderam 200 nos dois ambientes; versão exata `47c5f2fe37768d16c1a009b9b15688941e04bb02` | [Development](evidencias/smoke-development.json), [Production](evidencias/smoke-production.json) |
| Documentação restrita por ambiente | OpenAPI e Swagger responderam 404 em Production | [production-documentacao-restrita.json](evidencias/production-documentacao-restrita.json) |
| Artefato local rastreável | DLL e assets React publicados com commit/versão e SHA256 registrados | [artefato-local.json](evidencias/artefato-local.json) |
| Corpos HTTP reais | Sucesso 200, página vazia 200, validação 400 Problem Details e inexistente 404, com tipo de conteúdo/correlação | [http-contrato.json](evidencias/http-contrato.json) |
| Relatórios acadêmicos | Oito fontes Markdown e oito PDFs gerados com Chrome headless local, 37 páginas no total; relatório 01 incorpora as três fontes completas e relatórios pertinentes incorporam capturas reais | [manifesto PDF e SHA256](evidencias/relatorios-pdf.json) |
| Validação documental final | 248 identificadores únicos, links Markdown válidos, oito cabeçalhos PDF e anexo idêntico às três fontes atuais | [validacao-documental.json](evidencias/validacao-documental.json) |

Os 4 testes prioritários também pertencem à suíte cumulativa: não somar 4 e 55 como se fossem casos diferentes. O View Component recebeu uma verificação dirigida adicional, e a suíte SQL recebeu regressão do formulário MVC real. TestServer usa autenticação exclusiva de teste para isolar os roteiros iniciais; a suíte SQL verifica Identity/cookies reais separadamente. O smoke e os clientes HTTPS comprovam o transporte real, que TestServer não exercita.

Development utilizou User Secrets em perfil local isolado (`.local/secrets-profile`, ignorado pelo Git); Production recebeu variáveis externas. A publicação local utilizou o mesmo binário/código. A relação ao commit comprovada é a do artefato .NET/React local, não a de uma imagem Docker inexistente. O Git remoto está vazio e não há Docker instalado/serviço local: CI remoto, build/run/health do contêiner e CD por imagem não foram executados.

## Falhas encontradas e correções verificáveis

A captura MVC inicial revelou que o campo oculto `RowVersion` vazio no cadastro recebia validação `Required` implícita, gerando mensagem em inglês sobre versão. `TarefaFormModel.RowVersion` passou a ser anulável no formulário; edição continua exigindo versão válida explicitamente. A reprodução automatizada agora envia o mesmo campo vazio do navegador, e a integração SQL verifica o cadastro/edição. A recaptura corrigida foi concluída em 29/10 e está identificada na tabela; a captura inicial foi preservada separadamente como diagnóstico.

Tentativas SQL no sandbox falharam por SSL/SSPI; o mesmo servidor foi acessado e os testes executados fora do sandbox com autorização. Esse bloqueio foi resolvido para a suíte local e não deve ser confundido com indisponibilidade atual de SQL Server. A política de execução do Windows exigiu `Bypass` limitado ao processo dos scripts, sem alterar política global.

## Execuções reproduzíveis

Os comandos abaixo são procedimentos de reprodução; a presença nesta seção não afirma que todos já foram executados:

```powershell
dotnet restore PortalTarefas.sln
dotnet build PortalTarefas.sln -c Release --no-restore
dotnet test tests/PortalTarefas.Tests/PortalTarefas.Tests.csproj -c Release --filter 'FullyQualifiedName~DomainTests|FullyQualifiedName~InMemoryTests' --logger trx
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Integration.ps1 -Configuration Release -Initialize -ResultsDirectory docs/evidencias/testes-integracao
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Generate-Client.ps1 -ApiUrl https://localhost:7240
Push-Location src/PortalTarefas.Frontend
npm.cmd ci
npm.cmd test
npm.cmd run build
npm.cmd run smoke:client -- --api-url https://localhost:7240
Pop-Location
```

O banco da integração deve ter nome reservado `PortalTarefas_Tests_<sufixo>` e conexão externa `PORTAL_TEST_CONNECTION`. Os testes gravam dados e aplicam migrations exclusivamente nesse destino. Não há autorização para usar banco compartilhado como fixture ou apagá-lo.

## Pendências que não podem ser presumidas como concluídas

- Executar build/contêiner/Health Check quando Docker estiver disponível.
- Executar CI remoto, comprovar bloqueio de publicação em falha e validar implantação quando houver runner/destino autorizado.

Nenhum estado pendente nesta lista dispensa o requisito original. O relatório correspondente deve continuar explícito sobre sua limitação até haver prova de execução.

Marco final da documenta??o e das evid?ncias: tag local `auditoria/entrega-local-20261029`. O artefato executado continua vinculado ao commit de implementa??o `47c5f2fe37768d16c1a009b9b15688941e04bb02`, conforme os smokes e o manifesto de hashes.
