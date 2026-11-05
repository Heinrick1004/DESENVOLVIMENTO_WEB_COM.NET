# Preparação da entrega acadêmica

O projeto permanece na estrutura existente. Esta preparação não cria o ZIP definitivo, não remove arquivos da pasta de trabalho e não modifica imagens. O fechamento ocorrerá depois da etapa visual prevista em [PLANO_CAPTURAS_FINAIS.md](PLANO_CAPTURAS_FINAIS.md).

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
6. Repetir as verificações Docker/CI/CD somente em infraestrutura de teste disponível e autorizada.
7. Acrescentar as capturas finais em nova pasta e atualizar o inventário antes do ZIP definitivo.

Os marcos reais do Git são preservados no repositório de trabalho. Os oito marcos anteriores à auditoria não são reconstruídos artificialmente para a entrega.
