# Entrega acadêmica — Portal de Tarefas

Estado em 06/11/2026: **ENTREGA PREPARADA COM PENDÊNCIAS**. Há 55 testes .NET e 33 React aprovados, zero falhas/ignorados, build Release sem avisos, contrato real conferido, 12 capturas novas e oito relatórios Markdown/PDF. Permanecem R8-17 sem comprovação remota, CI próprio das alterações locais e limitações históricas G-02/G-03. O PR está draft, sem merge; detalhes em [status](STATUS_DE_EXECUCAO.md), [matriz](MATRIZ_DE_REQUISITOS.md), [revisão do PR](REVISAO_PR_1.md) e [E4.2](E4_2_SEGREDOS.md).

## Pacote e conteúdo

Arquivo: `artifacts/entrega/UNIP_Desenvolvimento_Web_NET_PortalTarefas_Entrega_Final.zip`. Há uma única pasta principal `PortalTarefas/`. O inventário interno `docs/INVENTARIO_ENTREGA.json` registra caminho, tamanho e SHA256 de cada fonte selecionada; não calcula hash de si mesmo. O ZIP é um snapshot das fontes locais atuais, derivado de `5bf8fef`, com mudanças ainda não commitadas identificadas pelos hashes. O SHA de origem não é apresentado como versão remota aprovada desse snapshot.

Inclui solução, projetos backend/frontend, migrations/SQL, testes, Dockerfile/Compose, workflow, scripts, OpenAPI e tipos, README, matriz/status, oito PDFs e Markdown/anexos, todas as evidências relevantes, 12 imagens finais e configurações de exemplo. A seleção usa arquivos versionados e novos não ignorados, excluindo caches, `bin`, `obj`, `dist`, `node_modules`, ferramentas baixadas, `.local`, `.nuget`, `.git`, saídas de publicação, User Secrets, certificados/chaves, arquivos privados de ambiente, bancos e backups. Não há limpeza de pastas nem remoção de dados/volumes existentes.

## Geração reproduzível e inspeção

Na raiz do repositório, com Git, PowerShell, Python 3.10+ e PyPDF2 3.0.1 instalados:

```powershell
python scripts/Validate-Documents.py
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Prepare-Delivery.ps1 -CreateZip
python scripts/Verify-Delivery.py artifacts/entrega/UNIP_Desenvolvimento_Web_NET_PortalTarefas_Entrega_Final.zip --output-dir artifacts/entrega/verificacao
```

O script de preparação recusa sobrescrever um ZIP existente; para outra execução, informe novo `-OutputDirectory` dentro do projeto. O verificador também recusa sobrescrever a pasta de inspeção. Ele valida integridade CRC, nomes/caminhos/seleção, inventário/hash, presença dos arquivos obrigatórios, oito PDFs, imagens e padrões conhecidos de segredos; extrai em diretório novo e registra relatórios externos ao ZIP. O tamanho/SHA256 efetivos estão em `artifacts/entrega/pacote.json`, repetidos no relatório de verificação. Os dados de integridade da própria entrega ficam fora do ZIP para evitar autorreferência.

## Validação da cópia extraída

O verificador informa o caminho do projeto extraído. Dentro dele:

```powershell
dotnet restore PortalTarefas.sln
dotnet build PortalTarefas.sln -c Release --no-restore
Push-Location src/PortalTarefas.Frontend
npm.cmd ci
npm.cmd test
npm.cmd run typecheck
npm.cmd run build
Pop-Location
python scripts/Validate-Documents.py --output artifacts/validacao-documental-extraida.json
```

Como alternativa reproduzível, a partir do projeto original, `scripts/Test-ExtractedDelivery.ps1 -ProjectPath <projeto-extraido> -ResultsDirectory <pasta-nova-de-resultados>` executa restore/build, restaura a ferramenta EF, roda os testes de domínio/memória e frontend, typecheck/build e a validação documental. Ele preserva os logs e recusa sobrescrever resultados anteriores.

Essas verificações independem de banco. A suíte completa de integração exige **outro banco SQL exclusivo** e configuração externa, conforme README; não executar testes SQL sem essa configuração e interpretar ignorados como aprovação. O contrato deve ser regenerado contra a API real, e não por edição manual. O README explica migrations, seed, HTTPS e acesso a MVC/React/API em uma instalação limpa. A fonte acadêmica `Roteiros.pdf` é externa ao projeto; a verificação opcional `--academic-pdf` permite fornecê-la novamente e não é pré-requisito de compilação.

A inspeção de segurança procura arquivos privados e padrões de credenciais, sem imprimir valores; achados potenciais precisam de revisão. O relatório registra o escopo e falsos positivos fundamentados. Não se promete ausência matemática de qualquer segredo desconhecido. O pacote só deve ser considerado verificado depois de conferir esse relatório e os resultados reais de restore/build/frontend na cópia.

## Ações indispensáveis para encerramento

1. Configurar o Actions Secret `PORTAL_CI_CERTIFICATE_PASSWORD` na conta, sem revelar seu valor.
2. Autorizar o envio das alterações revisadas à branch do PR e conferir o CI do novo SHA. Registrar a prova efetiva do cofre, HTTPS e saúde antes de concluir R8-17.
3. Autorizar o merge separadamente, caso desejado; verificar o CI do SHA integrado à `main`.
4. Atualizar o estado da entrega e gerar outro ZIP/inventário após eventuais alterações aprovadas. O pacote atual mantém as pendências identificadas; não reutilizar seu hash para outro conteúdo.

Nenhum ZIP foi publicado em serviço externo. As execuções históricas permanecem identificadas em seus diretórios; a revisão de 06/11 reconfirmou os runs original, negativo, recuperação e CI final remoto. O ensaio negativo continua válido para a ordem dos gates preservada. Os oito marcos anteriores de G-02/G-03 não foram fabricados.
