# Estado de execução — Portal de Tarefas

Consolidação documental: 05/11/2026, America/Sao_Paulo. Fonte acadêmica: `Roteiros.pdf`, 26 páginas, com releitura das páginas 24–26. Os registros preservam a data real de cada execução, inclusive as evidências de 28–29/10. O estado individual de cada exigência está na [matriz de requisitos](MATRIZ_DE_REQUISITOS.md); este arquivo consolida execuções, evidências e bloqueios.

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
| 8 | Configuração, Docker, CI e scripts de homologação | Docker, CI e implantação efêmera de teste aprovados no SHA `1da2eea`; V3 e E4.2 mantêm o roteiro parcial |

## Atualização confirmada em 05/11/2026

A consulta GET à API GitHub confirmou o [run 42117773881](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42117773881): workflow `Validar e empacotar Portal de Tarefas`, `main`, commit **`1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`**, estado `completed`, conclusão `success`. Início 05/11/2026 às 22:00:55 UTC e última atualização 22:03:18 UTC, equivalentes a 19:00:55–19:03:18 de São Paulo. [Resumo e limites da consulta](evidencias/auditoria-final-20261105/github/execucao-aprovada.json), [etapas](evidencias/auditoria-final-20261105/github/jobs-e-etapas.json) e [trechos do log](evidencias/auditoria-final-20261105/github/trechos-log.json) foram preservados sem credenciais.

| Verificação remota | Aprovados | Reprovados | Ignorados | Resultado |
|---|---:|---:|---:|---|
| Testes .NET com SQL/migrations exclusivos | 55 | 0 | 0 | Todas as suítes obrigatórias executadas |
| Testes React em cinco arquivos | 33 | 0 | 0 | Vitest aprovado |
| Restore/build .NET Release | — | — | — | Zero avisos e zero erros |
| npm ci, auditoria, typecheck/build e contrato/cliente | — | — | — | Etapas aprovadas |
| Build/execução da imagem com SQL/HTTPS e UID não zero | — | — | — | Etapas aprovadas |
| Smoke de live/ready, versão, MVC, SPA e assets | — | — | — | Script aprovado; exige HTTP 200 e SHA exato |
| Save/upload após verificações | — | — | — | Artefato publicável criado |

As duas etapas condicionais de diagnóstico de falha foram `skipped` porque o job teve sucesso; nenhum teste obrigatório foi ignorado. A evidência remota refere-se ao SHA acima. Mudanças posteriores no workflow precisam de uma nova execução para obter sua própria prova remota.

O [artefato 13456822922](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42117773881/artifacts/13456822922) chama-se `portaltarefas-1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`, possui 159.303.801 bytes e digest do ZIP `sha256:650797a7b10c8b54271d8d4885626f40b091ef59c251a47a404007643e711e2e`; a API informa expiração em **12/11/2026 às 22:02:52 UTC**. [Metadados](evidencias/auditoria-final-20261105/github/artefatos.json) e log de upload foram conferidos; o ZIP completo não foi extraído nesta consulta. O log de build registra ID Docker local `sha256:c80953ffbbb3ff59232094808f251b33fd40375fc9d376e48130221944e1f80c` e tag com o SHA completo. Isso não comprova publicação em registry.

| Situação separada | Estado e limite |
|---|---|
| Docker no Windows local | Bloqueio histórico de Engine/WSL preservado; ferramentas CLI/Compose não instalam o servidor |
| Docker no runner GitHub | Build e execução reais, usuário não privilegiado e smoke HTTP aprovados |
| Saúde nativa Docker | `.State.Health.Status` não foi explicitamente registrado no run aprovado; a nova espera de `healthy` aguarda execução remota |
| CI remoto | Aprovado no caminho de sucesso para o commit identificado |
| Falha controlada antes da publicação | [Ensaio preparado](ENSAIO_FALHA_CI.md), pendente de autorização para push/execução em branch experimental |
| Implantação em ambiente de teste (E4.3) | Concluída no runner efêmero: imagem com SQL/HTTPS e smoke/Health Checks HTTP aprovados, SHA identificado |
| Identidade federada/secret store do provedor (E4.2) | Pendente; configuração externa e senhas efêmeras do CI não demonstram o mecanismo do provedor do destino |
| Hospedagem persistente e promoção/rollback remotos | Não executados; o PDF não exige destino permanente e exige estratégia de retorno documentada, já existente |

A página 25 exige preparação de CD e implantação em ambiente de teste; o ambiente efêmero do runner atende E4.3 com evidência real de execução e smoke. Ele não representa hospedagem persistente, mas o PDF não exige permanência nem destino remoto para esse item. GHCR não é obrigatório. A página 26 apresenta staging com aprovação antes de produção como extensão alternativa; o inventário de dependências já foi selecionado. V3 e E4.2 permanecem pendentes; não se acrescentou infraestrutura externa sem necessidade ou autorização.

## Regressão e correções locais desta auditoria

Os testes normais foram repetidos em 05/11/2026, preservando o código da aplicação e usando bancos novos reservados. O artefato local informa o SHA de origem `1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`; mudanças documentais e do CI nesta auditoria não são apresentadas como nova execução remota desse artefato.

| Verificação local | Aprovados | Reprovados | Ignorados | Bloqueados | Evidência |
|---|---:|---:|---:|---:|---|
| Suíte .NET completa, SQL/Identity/CSRF/concorrência/observabilidade | 55 | 0 | 0 | 0 | [Resumo TRX](evidencias/auditoria-final-20261105/regressao/dotnet-testes/20261105T221626427Z-9a7a84b4/summary.json), [comando](evidencias/auditoria-final-20261105/regressao/dotnet-testes-comando.json) |
| Testes React em cinco arquivos | 33 | 0 | 0 | 0 | [Log](evidencias/auditoria-final-20261105/regressao/frontend-testes.txt), [comandos/resultados](evidencias/auditoria-final-20261105/regressao/frontend-resultados.json) |
| Ensaio intencional isolado, fora do produto | 0 | 1 esperada | 0 | 0 | [Resumo experimental](evidencias/auditoria-final-20261105/ensaio-local/resumo.json) |
| Recuperação após retirar o teste intencional, subconjunto domínio/memória | 29 | 0 | 0 | 0 | Mesmo resumo experimental; não somar ao total da suíte de 55 |

Restore e [build .NET Release](evidencias/auditoria-final-20261105/regressao/dotnet-build-release.json), typecheck e build frontend terminaram com código zero. O banco da suíte é `PortalTarefas_Tests_Final20261105_26b194a7`, distinto do banco novo de seed/homologação `PortalTarefas_Tests_FinalSeed20261105_0ee325ad`; nenhum banco/volume anterior foi removido. [Duas inicializações](evidencias/auditoria-final-20261105/regressao/seed-idempotente.json) mantiveram três tarefas, três auditorias, duas roles, dois usuários sintéticos e três migrations, sem duplicação.

O [contrato servido por HTTPS](evidencias/auditoria-final-20261105/regressao/contrato-real.json) e os tipos regenerados ficaram idênticos aos arquivos versionados. O [cliente de leitura](evidencias/auditoria-final-20261105/regressao/cliente-sql-https.txt) e o [fluxo autenticado CSRF/login/criação/logout](evidencias/auditoria-final-20261105/regressao/cliente-autenticado-https.txt) passaram com validação TLS preservada. A [homologação local do mesmo artefato](evidencias/auditoria-final-20261105/regressao/homologacao-local.json) aprovou live/ready, MVC e SPA com HTTP 200 em Development e Production; Swagger/OpenAPI retornaram 200 em Development e 404 em Production. Os processos próprios foram encerrados e os dados preservados.

A correção do CI acrescenta a espera explícita de `.State.Health.Status=healthy` antes de `docker save` e upload. A [validação estática atual](evidencias/ci-validacao-estatica.json) aprovou 31 etapas YAML, 22 blocos Bash, seis scripts e quatro blocos PowerShell. Os [três cenários locais do novo gate](evidencias/auditoria-final-20261105/gate-health-local.json) passaram com funções Docker/sleep simuladas: `healthy` aceita, `unhealthy` e timeout rejeitam. Isso valida a lógica do bloco; não constitui execução de contêiner real nem substitui uma nova execução GitHub.

A [rechecagem Docker local](evidencias/auditoria-final-20261105/docker-local.json) confirmou CLI/Compose e configuração sintética válidos, mas Engine indisponível. O ensaio remoto depende de autorização e E4.2 depende de definição do mecanismo de segredos/identidade do provedor; nenhuma suíte normal foi aceita com testes ignorados. Não houve novas capturas de tela.

## Resultados históricos preservados — 28–29/10/2026

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
| Validação estática CI/scripts de outubro | Sintaxe YAML/Bash/PowerShell e ordem de gates conferidas; registro histórico preservado, distinto da nova validação de 31 etapas | [ci-validacao-estatica.json histórico](evidencias/finalizacao-r8-20261029/ci-validacao-estatica.json) |
| Inventários locais | Dependências .NET/npm geradas e preservadas | [.NET](evidencias/dependencias-dotnet.json), [npm](evidencias/dependencias-npm.json) |
| Revisão de arquivos/credenciais | 199 arquivos revisados, nenhum nome de arquivo sensível ou padrão conhecido de credencial encontrado; limites do método explícitos | [revisao-segredos.json](evidencias/revisao-segredos.json) |
| Development/Production simulada no mesmo artefato | Live, ready, MVC e SPA responderam 200 nos dois ambientes; versão exata `47c5f2fe37768d16c1a009b9b15688941e04bb02` | [Development](evidencias/smoke-development.json), [Production](evidencias/smoke-production.json) |
| Documentação restrita por ambiente | OpenAPI e Swagger responderam 404 em Production | [production-documentacao-restrita.json](evidencias/production-documentacao-restrita.json) |
| Artefato local rastreável | DLL e assets React publicados com commit/versão e SHA256 registrados | [artefato-local.json](evidencias/artefato-local.json) |
| Corpos HTTP reais | Sucesso 200, página vazia 200, validação 400 Problem Details e inexistente 404, com tipo de conteúdo/correlação | [http-contrato.json](evidencias/http-contrato.json) |
| Relatórios acadêmicos | Oito fontes Markdown e oito PDFs gerados com Chrome headless local, 37 páginas no total; relatório 01 incorpora as três fontes completas e relatórios pertinentes incorporam capturas reais | [manifesto PDF e SHA256](evidencias/relatorios-pdf.json) |
| Validação documental final | 248 identificadores únicos, links Markdown válidos, oito cabeçalhos PDF e anexo idêntico às três fontes atuais | [validacao-documental.json](evidencias/validacao-documental.json) |

Os 4 testes prioritários também pertencem à suíte cumulativa: não somar 4 e 55 como se fossem casos diferentes. O View Component recebeu uma verificação dirigida adicional, e a suíte SQL recebeu regressão do formulário MVC real. TestServer usa autenticação exclusiva de teste para isolar os roteiros iniciais; a suíte SQL verifica Identity/cookies reais separadamente. O smoke e os clientes HTTPS comprovam o transporte real, que TestServer não exercita.

Na validação de outubro, Development utilizou User Secrets em perfil local isolado (`.local/secrets-profile`, ignorado pelo Git); Production recebeu variáveis externas. A publicação local utilizou o mesmo binário/código. Naquela data, a relação ao commit comprovada era somente a do artefato .NET/React local: o remoto estava vazio, e não havia Engine/serviço Docker local. A ausência de execução Docker/CI e de teste da imagem foi resolvida no runner em 05/11, conforme a seção atual; o bloqueio local e a falta de hospedagem persistente têm escopos distintos. A permanência do destino não é critério obrigatório do PDF.

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

- Reproduzir Docker no Windows quando o Engine estiver disponível, sem desconsiderar o build/execução/smoke já aprovados no runner.
- Executar remotamente a nova espera do estado nativo Docker `healthy`, acrescentada após o run aprovado.
- Autorizar e executar o ensaio isolado que comprova bloqueio de publicação após falha, mantendo `main` sem o teste intencionalmente defeituoso.
- Definir e comprovar identidade federada ou secret store do provedor para E4.2, mantendo conexões e credenciais externas. O ambiente efêmero de teste, smoke e estratégia de retorno já possuem evidência; um destino adicional de hospedagem exige autorização própria.
- Produzir as novas capturas finais somente na etapa visual posterior; evidências históricas permanecem preservadas.

Nenhum estado pendente nesta lista dispensa o requisito original. O relatório correspondente deve continuar explícito sobre sua limitação até haver prova de execução.

Marco da auditoria anterior: tag local `auditoria/entrega-local-20261029`. O artefato executado naquela validação está vinculado ao commit de implementação `47c5f2fe37768d16c1a009b9b15688941e04bb02`, conforme os smokes e o manifesto de hashes preservados.
