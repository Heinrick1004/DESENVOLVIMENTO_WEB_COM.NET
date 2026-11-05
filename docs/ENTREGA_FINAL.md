# Preparação da entrega acadêmica

O projeto permanece na estrutura existente. Esta preparação não cria o ZIP definitivo, não remove arquivos da pasta de trabalho e não modifica imagens. O fechamento ocorrerá depois da etapa visual prevista em [PLANO_CAPTURAS_FINAIS.md](PLANO_CAPTURAS_FINAIS.md).

## Atualização de evidências — 05/11/2026

O [run GitHub 42117773881](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42117773881), commit `1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`, foi reconfirmado com `success`: 55 testes .NET e 33 React aprovados, nenhum falho/ignorado, imagem Docker construída/executada com SQL/HTTPS e smoke aprovado. Os [registros sanitizados da consulta](evidencias/auditoria-final-20261105/github/execucao-aprovada.json) devem acompanhar a entrega. O artefato `portaltarefas-1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`, ID `13456822922`, tem retenção até 12/11/2026 às 22:02:52 UTC; o estado registrado não garante disponibilidade após a expiração.

O bloqueio Docker no Windows pertence ao ambiente local, e não invalida a execução no runner. A implantação efêmera de teste com SQL/HTTPS e smoke atende E4.3; o PDF não exige destino persistente/remoto. O [ensaio negativo remoto](ENSAIO_FALHA_CI.md) comprovou V3: falha sintética, etapas de entrega skipped e ausência do artefato publicável. A [recuperação 42122508347](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122508347) foi aprovada após a reversão, com 55 testes .NET e 33 React sem falhos/ignorados e entrega validada. O roteiro 8 continua parcial por E4.2, identidade federada/secret store do provedor ainda não demonstrado. O novo gate nativo Docker foi aprovado no [CI do PR 42122467632](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122467632), head bfc46abb e merge sintético/imagem 3f4c18a0; [inferência e limites](evidencias/auditoria-final-20261105/github/ci-saude-nativa-trechos-log.json) estão registrados. Hospedagem persistente e promoção/rollback remotos não foram realizados, sem acrescentá-los como critérios obrigatórios do PDF.

O relatório `08-Entrega.md` e seu PDF devem refletir essa distinção. Os relatórios 1–7, seus PDFs e todas as imagens anteriores permanecem preservados. Nenhuma nova captura é produzida nesta auditoria.

## Arquivos essenciais

| Grupo | Manter na entrega | Motivo |
|---|---|---|
| Solução e ferramentas | `PortalTarefas.sln`, `global.json`, `NuGet.Config`, `.config/dotnet-tools.json` | Seleção do SDK, projetos, fontes de pacotes e ferramenta EF restaurável |
| Backend | `src/PortalTarefas.Web/`, exceto saídas de build | Código, Razor, CSS, modelos, DTOs, validações, serviços, Identity e infraestrutura |
| Migrations | `src/PortalTarefas.Web/Data/Migrations/` e `docs/sql/migrations.sql` | Histórico e revisão do esquema; não podem ser substituídos pelo banco local |
| Frontend | Fontes React, `public/`, `index.html`, configurações Vite/TypeScript, `package.json` e `package-lock.json` | Restauração e build reproduzíveis |
| Contrato | `docs/openapi.json`, `src/PortalTarefas.Frontend/src/api/schema.d.ts` e cliente | Artefatos gerados versionados usados pelo cliente; não são cache descartável |
| Testes | `tests/PortalTarefas.Tests/` e testes frontend | Validação de domínio, HTTP, SQL, autenticação, concorrência, interface e infraestrutura |
| Operação | `Dockerfile`, `compose.yaml`, `.dockerignore`, `.github/workflows/` e `scripts/` | Configuração de contêiner, CI e procedimentos reproduzíveis |
| Documentação | `README.md`, matriz, status, plano visual e documentos de ambiente/entrega | Execução, rastreabilidade, bloqueios e procedimentos |
| Relatórios | Oito fontes Markdown, oito PDFs e anexos exigidos | Entrega acadêmica e código completo documentado |
| Evidências reais | `docs/evidencias/`, inclusive imagens históricas, manifests, logs sanitizados, TRX e inventários | Provas reais e limites das verificações; preservar a origem de cada resultado |
| Regras do repositório | `.gitignore` e configurações necessárias | Separação entre fontes, saídas restauráveis e arquivos privados |

O inventário detalhado é produzido por `scripts/Prepare-Delivery.ps1` em `INVENTARIO_ENTREGA.json`, identificando os arquivos essenciais, tamanho/SHA256 e os grupos restauráveis. Ele não inclui valores secretos nem funciona como cópia de banco. Execute-o depois das últimas alterações e novamente após acrescentar as capturas futuras:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Prepare-Delivery.ps1
```

O script somente lê fontes e grava o índice. Não cria ZIP, não copia a árvore, não remove caches e não altera imagens/relatórios. O índice inclui arquivos novos não ignorados; o commit observado na geração não é apresentado como commit final de arquivos ainda não commitados.

## Arquivos restauráveis ou privados que não devem compor o ZIP futuro

- Saídas `bin/`, `obj/`, `dist/`, `artifacts/` e `TestResults/` quando os resultados acadêmicos pertinentes já estiverem preservados em `docs/evidencias/`.
- `node_modules/`, caches NuGet/npm, ferramentas portáteis baixadas e seus arquivos de instalação.
- `.local/`, `.tools/`, `.nuget/`, perfis temporários, publicações de teste e caches de navegador.
- `wwwroot/app/` quando contiver somente a cópia restaurável do build React; manter as fontes e o procedimento de integração.
- Senhas, tokens, User Secrets, arquivos externos de ambiente com valores reais, certificados privados/PFX/chaves, arquivos de banco e backups locais.

Essa lista define o conteúdo da entrega futura; não autoriza apagar as pastas atuais. Os logs/evidências essenciais não devem ser removidos apenas por terem sido gerados por ferramenta.

## Restauração e validação da cópia de entrega

1. Instalar as versões documentadas de .NET e Node, restaurar a ferramenta EF e executar `dotnet restore` e `npm ci`.
2. Configurar a conexão e as contas sintéticas externamente, mantendo desenvolvimento e integração em bancos distintos e reservados.
3. Aplicar migrations/seed de forma explícita e controlada. Não copiar banco local como substituto das migrations.
4. Compilar backend e frontend; executar os testes conforme o README. Integração SQL obrigatória não pode ser aceita como ignorada.
5. Validar o contrato gerado e os clientes contra a API real quando o ambiente permitir.
6. Usar o run aprovado como evidência do commit identificado e do ambiente efêmero de teste; preservar as evidências do ensaio negativo autorizado e da recuperação aprovada; validar alterações posteriores com autorização para ações remotas. Comprovar E4.2 quando o mecanismo de segredos/identidade do provedor estiver definido; hospedagem adicional depende de autorização própria.
7. Acrescentar as capturas finais em nova pasta e atualizar o inventário antes do ZIP definitivo.

Os marcos reais do Git são preservados no repositório de trabalho. Os oito marcos anteriores à auditoria não são reconstruídos artificialmente para a entrega.
