# Portal de Tarefas — Desenvolvimento Web com .NET

Aplicação acadêmica cumulativa da UNIP: ASP.NET Core MVC e API REST, EF Core com SQL Server, SPA React/TypeScript, Identity, observabilidade e empacotamento. A solução existente foi preservada. Os oito relatórios descrevem a implementação atual; o estado de execução de cada requisito está em [docs/MATRIZ_DE_REQUISITOS.md](docs/MATRIZ_DE_REQUISITOS.md) e [docs/STATUS_DE_EXECUCAO.md](docs/STATUS_DE_EXECUCAO.md).

## Pré-requisitos e versões

- Windows/PowerShell ou ambiente compatível com o .NET SDK **10.0.403**, selecionado em `global.json`; alvo `net10.0`.
- ASP.NET Core e EF Core **10.0.14**, conforme os arquivos `.csproj`.
- Node.js **24.22.1** e npm; as versões efetivamente executadas ficam nas evidências. `package-lock.json` fixa a árvore npm.
- SQL Server com permissão para criar um banco de desenvolvimento e outro exclusivo para testes. É possível usar a conexão da extensão SQL Server do VS Code para inspecionar esses bancos; a aplicação e os testes recebem a conexão pela configuração.
- Git. Docker é necessário somente para executar as verificações de contêiner.

No ambiente de trabalho existe Node portátil em `.tools/node-v24.22.1-win-x64`; ele é ignorado pelo Git. Para utilizá-lo nesta sessão:

```powershell
$env:PATH = (Join-Path $PWD '.tools/node-v24.22.1-win-x64') + [IO.Path]::PathSeparator + $env:PATH
dotnet --version
node --version
npm.cmd --version
```

Todos os comandos seguintes partem da pasta `PortalTarefas`.

## Estrutura

```text
src/PortalTarefas.Web/       MVC, API, Identity, EF Core e infraestrutura
src/PortalTarefas.Frontend/  React, contrato gerado, cliente e testes
tests/PortalTarefas.Tests/   domínio, memória, HTTP, SQL e observabilidade
scripts/                   geração do cliente, validação e publicação local
docs/relatorios/            relatórios numerados 01 a 08 e anexos
docs/evidencias/            resultados reais de execução, sem credenciais
docs/sql/migrations.sql     SQL de migrations para revisão
.github/workflows/ci.yml    validação e empacotamento no CI
```

## Configuração do SQL Server e User Secrets

O projeto Web já declara `UserSecretsId`. Armazene a conexão de **desenvolvimento** em User Secrets. O exemplo usa autenticação integrada do Windows e um banco específico de desenvolvimento:

```powershell
dotnet user-secrets set 'ConnectionStrings:PortalTarefas' 'Server=localhost;Database=PortalTarefas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True' --project src/PortalTarefas.Web
```

`TrustServerCertificate=True` serve ao certificado local de desenvolvimento. Em outro ambiente, configure certificado e cadeia de confiança adequados. Para SQL Authentication, informe a conexão no secret store local; nunca a inclua em arquivos versionados. User Secrets é um recurso local de desenvolvimento, não um cofre de produção.

Crie contas de demonstração com valores externos. O seed não possui senha fixa e não redefine a senha de uma conta já existente. Exemplo sem senha literal no arquivo:

```powershell
dotnet user-secrets set 'Seed:AdminEmail' 'admin@portal.example' --project src/PortalTarefas.Web
dotnet user-secrets set 'Seed:AdminEquipe' 'Geral' --project src/PortalTarefas.Web
$senhaPortal = [System.Net.NetworkCredential]::new('', (Read-Host 'Senha administrativa local (mínimo 12 caracteres, maiúscula, minúscula, número e símbolo)' -AsSecureString)).Password
dotnet user-secrets set 'Seed:AdminPassword' $senhaPortal --project src/PortalTarefas.Web
Remove-Variable senhaPortal
dotnet user-secrets set 'Seed:SampleData' 'true' --project src/PortalTarefas.Web
```

Para conta comum, configure também `Seed:UserEmail`, `Seed:UserPassword` e `Seed:UserEquipe`. Execute a inicialização explicitamente; ela aplica migrations e seed ao banco indicado pela conexão:

```powershell
dotnet restore PortalTarefas.sln
dotnet build PortalTarefas.sln -c Release --no-restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/PortalTarefas.Web --no-launch-profile -- --initialize
dotnet run --project src/PortalTarefas.Web --launch-profile https
```

O banco de desenvolvimento deve ser escolhido conscientemente antes de `--initialize`. Os testes possuem configuração própria e não devem receber a conexão de desenvolvimento. A lista das migrations está em `src/PortalTarefas.Web/Data/Migrations`; o SQL revisável está em `docs/sql/migrations.sql`.

## Execução e interfaces

Confie no certificado local quando necessário:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/PortalTarefas.Web --launch-profile https
```

- MVC: `https://localhost:7240/Tarefas/Index`.
- Swagger UI, somente Development: `https://localhost:7240/swagger`.
- OpenAPI, somente Development: `https://localhost:7240/openapi/v1.json`.
- Liveness/readiness: `/health/live` e `/health/ready`.
- Diagnósticos equivalentes: `/api/minimal/diagnostico/42?filtro=aberta` e `/api/diagnostico/42?filtro=aberta`, com cabeçalho opcional `X-Cliente`.

Em outro terminal:

```powershell
Set-Location src/PortalTarefas.Frontend
npm.cmd ci
npm.cmd run dev
```

O Vite usa porta 5173 e encaminha `/api` para `https://localhost:7240`; a URL pode ser configurada com `VITE_API_URL` conforme `.env.example`. A configuração de desenvolvimento do proxy admite o certificado local da API. Cookies da aplicação usam `Secure`; para testar autenticação completa use uma origem HTTPS compatível com os cookies do navegador ou o build servido pela própria API em `/app/`. Não remova `Secure` para contornar um ambiente HTTP.

Para preparar a SPA integrada à API:

```powershell
Push-Location src/PortalTarefas.Frontend
npm.cmd run build
Pop-Location
New-Item -ItemType Directory -Path src/PortalTarefas.Web/wwwroot/app -Force | Out-Null
Copy-Item -Path src/PortalTarefas.Frontend/dist/* -Destination src/PortalTarefas.Web/wwwroot/app -Recurse -Force
```

## Contrato OpenAPI e cliente TypeScript

`docs/openapi.json` deve vir de uma aplicação em execução; `src/api/schema.d.ts` é gerado desse documento. Não edite os tipos gerados manualmente. Com a API em Development e as dependências npm instaladas:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Generate-Client.ps1 -ApiUrl https://localhost:7240
Push-Location src/PortalTarefas.Frontend
npm.cmd run smoke:client -- --api-url https://localhost:7240
npm.cmd run typecheck
Pop-Location
```

O script também aceita `-DocumentUrl` para a URL completa do documento. Confie no certificado local antes de chamar a API por HTTPS. O cliente usa `openapi-fetch` com tipos gerados; tipos de apresentação e filtros não devem duplicar manualmente o contrato HTTP. Alterações de DTO/rota exigem regeneração, revisão do diff e novo build.

Para o Node validar o certificado local, exporte somente sua parte pública em PEM e configure `NODE_EXTRA_CA_CERTS` com esse arquivo antes do comando de smoke. A execução auditada usou essa CA pública no processo, preservando a validação TLS. Não use a chave privada/PFX como CA e não desative a verificação TLS globalmente.

## Regras do domínio e permissões

- Título: de 3 a 120 caracteres; descrição opcional com até 2.000; equipe obrigatória com até 80.
- Situações: `Pendente`, `EmAndamento`, `Concluida`. Prioridades: 1 a 3. Não existe transição restrita de situação além desses valores permitidos.
- Prioridade 3 exige prazo e descrição com pelo menos dez caracteres úteis. Prazo usa data civil; uma tarefa está vencida quando tem prazo anterior à data de São Paulo e não está concluída. Datas de criação/auditoria são instantes UTC.
- O servidor atribui proprietário e instante de criação. O cliente recebe `rowVersion` em Base64 e devolve a versão originalmente lida ao editar/excluir. Versão inválida produz 400; versão obsoleta produz 409. Recarregue os dados antes de reaplicar alterações conscientemente.
- Paginação: `pagina=1`, `tamanho=10`, máximo 50; ordenação padrão `prazo asc`, com desempate por identificador. Campos permitidos: `prazo`, `titulo`, `prioridade`, `situacao`, `criadaem`, `id`. Filtros: `busca`, `situacao`, `prioridade`, `vencidas`.

| Operação | Anônimo | Usuário | Administrador do seed |
|---|---|---|---|
| Ler tarefa pública | Sim | Sim | Sim |
| Ler tarefa privada | Não | Da própria equipe | Todas |
| Criar | Não | Na própria equipe | Em qualquer equipe |
| Editar | Não | Equipe/proprietário; outra equipe exige claim | Recebe a claim de edição entre equipes |
| Excluir | Não | Não | Sim, com versão atual |

A claim `tarefas:editar-outra-equipe=true` permite edição entre equipes e transferência para outra equipe. Ela não concede leitura privada de outra equipe automaticamente. A role Administrador concede leitura privada e exclusão; o seed também lhe atribui a claim. Listagens, detalhes, contagens e painéis aplicam a visibilidade no serviço. Recurso privado não visível responde 404 na leitura; escrita autenticada sem permissão responde 403. A interface apenas reflete essas regras.

## Autenticação e CSRF

O cenário principal é navegador de mesma origem: cookie Identity `HttpOnly`, `Secure`, `SameSite=Lax`, com expiração de 30 minutos e renovação deslizante. Cinco tentativas incorretas bloqueiam temporariamente a conta por cinco minutos. Não existe cadastro público ou servidor OAuth/OIDC no escopo atual.

O login MVC fica em `/Conta/Login`. A SPA utiliza `/api/auth/csrf`, `/api/auth/login`, `/api/auth/me` e `/api/auth/logout`. Em operações de escrita, o cliente envia `X-CSRF-TOKEN`; deve renová-lo após login/logout porque a identidade da sessão mudou. A API retorna 401/403, enquanto MVC pode redirecionar para login/acesso negado. Use as contas configuradas no seed; credenciais dos testes são aleatórias e não servem como conta fixa de demonstração.

## Testes e banco exclusivo de integração

Testes unitários/domínio/memória:

```powershell
dotnet test tests/PortalTarefas.Tests/PortalTarefas.Tests.csproj -c Release --filter 'FullyQualifiedName~DomainTests|FullyQualifiedName~InMemoryTests' --logger trx
```

Para integração, prepare um banco **novo e exclusivo** com nome como `PortalTarefas_Tests_Local20261028`. A conexão deve vir do ambiente e apontar para esse banco. A suíte aplica migrations e grava fixtures; nunca use um banco compartilhado, de desenvolvimento ou produção.

```powershell
$env:PORTAL_TEST_CONNECTION = 'Server=localhost;Database=PortalTarefas_Tests_Local20261028;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Integration.ps1 -Configuration Release -Initialize -ResultsDirectory docs/evidencias/testes-integracao
```

O script exige nome de banco reservado para teste e valida o TRX: falhas, testes ignorados e ausência das suítes obrigatórias impedem aprovação. Um `dotnet test` comum pode registrar `[SqlFact]` como ignorado quando não há conexão; isso não comprova API, SQL Server ou Identity. Os resultados devem distinguir aprovados, reprovados e ignorados.

O parâmetro `-Initialize` aplica migrations/seed explicitamente no banco guardado. `-NoBuild` pode ser usado depois de um build Release atualizado. A política `Bypass` acima vale somente para o processo PowerShell local que executa o script; não altera a política global. Em PowerShell 7 com política compatível, use `pwsh -File` ou chamada direta. A ferramenta EF também é restaurável por `dotnet tool restore`, usando `.config/dotnet-tools.json`.

Frontend:

```powershell
Push-Location src/PortalTarefas.Frontend
npm.cmd ci
npm.cmd test
npm.cmd run build
Pop-Location
```

## Configuração externa

| Chave | Uso |
|---|---|
| `ConnectionStrings__PortalTarefas` | Conexão da aplicação fora de User Secrets |
| `ASPNETCORE_ENVIRONMENT` | `Development`, `Testing` ou `Production` |
| `Portal__Storage` | `SqlServer`; `Memory` apenas Development/Testing |
| `Portal__FrontendOrigin` | Origem explícita permitida pelo CORS |
| `Seed__Enabled` | Ativar seed na inicialização normal; preferir inicialização explícita |
| `Seed__SampleData` | Criar exemplos opcionais de forma idempotente |
| `Seed__AdminEmail`, `Seed__AdminPassword`, `Seed__AdminEquipe` | Conta administrativa opcional |
| `Seed__UserEmail`, `Seed__UserPassword`, `Seed__UserEquipe` | Conta comum opcional |
| `Telemetry__Console` | Exportar métricas/traces para console |
| `Dependencia__Url` | Dependência HTTP essencial opcional, usada no teste de readiness |
| `PORTAL_TEST_CONNECTION` | Conexão exclusiva da suíte de integração |
| `VITE_API_URL` | URL base pública da API; nunca recebe segredo |

Em configuração hierárquica de ambiente, `__` representa `:`. `PortalOptions` é validado ao iniciar. Sem conexão válida para o modo SQL Server, a aplicação deve falhar com mensagem segura. `Memory` contém somente tarefas voláteis: não substitui o banco das verificações relacionais e o login Identity continua exigindo SQL Server.

## Docker, CI e operação

O Dockerfile compila React e o inclui em `wwwroot/app`, publica .NET em múltiplos estágios e executa com usuário sem privilégios. A estratégia integrada simplifica cookies e implantação para este domínio. Uma SPA em imagem separada exigiria configurar origem, proxy e sessão entre as aplicações.

O `compose.yaml` declara SQL Server e Web, volumes de dados e chaves Data Protection, certificado externo e Health Checks. Configure externamente `MSSQL_SA_PASSWORD`, `PORTAL_SQL_CONNECTION`, `PORTAL_HTTPS_CERTIFICATE_PATH`, `PORTAL_HTTPS_CERTIFICATE_PASSWORD` e, se desejado, `PORTAL_IMAGE_TAG`/contas seed. A conexão interna usa host `db`, porta 1433. Os arquivos de ambiente/certificados devem permanecer fora do Git.

```powershell
docker compose config --quiet
docker compose up -d db
docker compose build web
docker compose run --rm web --initialize
docker compose up -d web
./scripts/Smoke-Test.ps1 -BaseUrl https://localhost:8443
```

O CI deve provisionar um SQL Server de teste, executar migrations/integração, contrato gerado, testes e builds antes do empacotamento. Imagem e artefatos são identificados pelo commit. O workflow existente é preparação verificável; execução remota exige repositório e runner. Nenhum push/deploy remoto é realizado por esta correção.

Para homologação local, `Publish-Local.ps1` recebe imagem identificada, arquivo externo de ambiente e PFX, preserva a instância anterior e chama o smoke test. Promoção: aprovar o mesmo artefato validado, mantendo tag/commit e configuração externa. Rollback: redirecionar para a imagem anterior preservada; migrations exigem avaliação própria e backup antes de mudanças incompatíveis. Não remova volumes para fazer rollback.

## Relatórios, evidências e marcos

Os arquivos 01 a 08 ficam em `docs/relatorios`. A matriz identifica página e item original do PDF, implementação, procedimento, evidência e estado. Uma atividade permanece parcial enquanto faltar uma verificação obrigatória, mesmo que todos os arquivos estejam presentes. Capturas não realizadas são pendências explícitas; não são substituídas por imagens artificiais.

Relatórios em PDF: [01 — Pipeline](docs/relatorios/01-Pipeline.pdf), [02 — MVC](docs/relatorios/02-MVC.pdf), [03 — Persistência](docs/relatorios/03-Persistencia.pdf), [04 — API](docs/relatorios/04-API.pdf), [05 — React](docs/relatorios/05-React.pdf), [06 — Identity](docs/relatorios/06-Identity.pdf), [07 — Observabilidade](docs/relatorios/07-Observabilidade.pdf) e [08 — Entrega](docs/relatorios/08-Entrega.pdf). O PDF 01 incorpora o anexo com código completo; capturas reais são incorporadas nos relatórios pertinentes.

O marco `auditoria/base-existente-20261028` preserva a base recebida; as correções ficam na branch `correcoes/auditoria-20261028`. Marcos posteriores representam verificações atuais da solução cumulativa, sem fabricar uma evolução histórica de oito versões.

## Solução de problemas

- **SDK não encontrado:** confira `dotnet --list-sdks` e `global.json`; não retargete a solução para outra versão para esconder a ausência do SDK.
- **SQL/SSPI ou certificado:** confira serviço, instância, autenticação e acesso do processo. A conexão que funciona na extensão VS Code precisa estar acessível também ao processo .NET. Limitações do sandbox devem ser registradas separadamente de indisponibilidade do servidor.
- **400 no login/escrita:** obtenha CSRF e envie o cabeçalho junto com os cookies; renove após mudança de identidade.
- **Cookie não persiste:** use HTTPS e verifique origem/proxy. Não relaxe atributos de segurança em produção.
- **409 na edição:** outra sessão alterou o recurso; preserve os valores digitados e recarregue a versão atual.
- **Tipos TypeScript ausentes:** execute `Generate-Client.ps1` contra a API real e depois `npm run typecheck`.
- **Readiness 503:** verifique SQL e a dependência HTTP opcional. Liveness pode continuar 200 porque mede a resposta do processo.
- **Docker indisponível:** registre o bloqueio e mantenha build/execução do contêiner pendentes. Um Dockerfile existente não comprova uma imagem executada.
