# Entrega acadêmica — Portal de Tarefas

Estado após comprovação de E4.2 em 06/11/2026: **ENTREGA PREPARADA COM PENDÊNCIAS HISTÓRICAS**. A matriz registra 233 concluídos e verificados, 13 não aplicáveis fundamentados e duas limitações históricas (G-02/G-03). Há 55 testes .NET e 33 React aprovados, zero falhas/ignorados, 12 capturas reais e oito relatórios Markdown/PDF. E4.2 foi comprovado no [run 42313806838, tentativa 2](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42313806838/attempts/2); os marcos anteriores G-02/G-03 continuam sem comprovação retroativa. O PR permanece sem merge. [Status](STATUS_DE_EXECUCAO.md), [matriz](MATRIZ_DE_REQUISITOS.md), [revisão do PR](REVISAO_PR_1.md) e [E4.2](E4_2_SEGREDOS.md) identificam evidências e limites.

## Pacote e conteúdo

Arquivo da nova rodada: `artifacts/entrega-e4-2-20261106/UNIP_Desenvolvimento_Web_NET_PortalTarefas_Entrega_Final.zip`. Há uma única pasta principal `PortalTarefas/`. As fontes versionadas devem estar limpas e corresponder ao snapshot Git indicado no manifesto do pacote, com CI próprio aprovado antes da entrega. O inventário interno `docs/INVENTARIO_ENTREGA.json` é metadado gerado no empacotamento, registra caminho, tamanho e SHA256 de cada fonte selecionada e não calcula hash de si mesmo. O pacote anterior em `artifacts/entrega/` permanece histórico, com suas pendências e hashes; não é sobrescrito.

Inclui solução, projetos backend/frontend, migrations/SQL, testes, Dockerfile/Compose, workflow, scripts, OpenAPI e tipos, README, matriz/status, oito PDFs e Markdown/anexos, todas as evidências relevantes, 12 imagens finais e configurações de exemplo. A seleção usa arquivos versionados e novos não ignorados, excluindo caches, `bin`, `obj`, `dist`, `node_modules`, ferramentas baixadas, `.local`, `.nuget`, `.git`, saídas de publicação, User Secrets, certificados/chaves, arquivos privados de ambiente, bancos e backups. Não há limpeza de pastas nem remoção de dados/volumes existentes.

## Geração reproduzível e inspeção

Na raiz do repositório, com Git, PowerShell, Python 3.10+ e PyPDF2 3.0.1 instalados:

```powershell
python scripts/Validate-Documents.py
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Prepare-Delivery.ps1 -CreateZip -OutputDirectory artifacts/entrega-e4-2-20261106
python scripts/Verify-Delivery.py artifacts/entrega-e4-2-20261106/UNIP_Desenvolvimento_Web_NET_PortalTarefas_Entrega_Final.zip --output-dir artifacts/entrega-e4-2-20261106/verificacao
```

O script de preparação recusa sobrescrever um ZIP existente; para outra execução, informe novo `-OutputDirectory` dentro do projeto. O verificador também recusa sobrescrever a pasta de inspeção. Ele valida integridade CRC, nomes/caminhos/seleção, inventário/hash, presença dos arquivos obrigatórios, oito PDFs, imagens e padrões conhecidos de segredos; extrai em diretório novo e registra relatórios externos ao ZIP. O tamanho/SHA256 efetivos estão em `artifacts/entrega-e4-2-20261106/pacote.json`, repetidos no relatório de verificação. Os dados de integridade da própria entrega ficam fora do ZIP para evitar autorreferência.

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

## Validação da revisão empacotada e integração

O [CI 42313806838, tentativa 2](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42313806838/attempts/2) terminou aprovado para o head `90ad9a2f6241a520df38d3be9b77a60fc76df9db`, testado no merge sintético `731aadb3d531e22820f086f472f7ddad8b5151db`. Foram aprovados 55 testes .NET e 33 React, zero falhos e zero ignorados, além de SQL/migrations, contrato/cliente, builds, Docker com HTTPS e usuário não privilegiado, smoke, saúde nativa e save/upload. A etapa sem fallback consumiu o Actions Secret para gerar o PFX; o contêiner abriu esse certificado e respondeu por HTTPS, com manifesto nativo `healthy`. O agente não consultou o valor do segredo; ele não foi incluído nas evidências, imagem ou pacote. A [comprovação sanitizada](evidencias/e4-2-20261106/github/comprovacao-e4-2.json) fundamenta a mudança de R8-17. A aprovação remota da revisão efetivamente empacotada deve ser conferida nos checks do PR e no comprovante final associado ao SHA registrado no manifesto do pacote. A execução citada comprova seus próprios head e checkout; não aprova automaticamente commits posteriores. O comprovante do CI final e os dados de integridade do ZIP ficam nos arquivos de acompanhamento ao lado do pacote, sem alterar novamente as fontes versionadas.

A [tentativa 1](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42313806838/attempts/1) foi reprovada pela guarda que exige ao menos 16 caracteres no Actions Secret, antes de executar o contêiner, smoke, saúde nativa e entrega. Os testes anteriores da execução haviam passado; essa rejeição foi de configuração. O responsável informou a correção do segredo e a tentativa 2 reexecutou o mesmo head/checkout. Os [registros da primeira tentativa](evidencias/e4-2-20261106/github/comprovacao-e4-2-attempt1.json) permanecem preservados; não são substituídos pelo resultado aprovado posterior.

Depois da consolidação documental, conferir o CI do head realmente selecionado para o pacote e seu checkout sintético. Gerar o ZIP a partir das fontes versionadas limpas; guardar a prova desse CI, `pacote.json`, inventário e verificações externas ao ZIP na mesma pasta de entrega. Metadados pós-commit são identificados como tais e não se atribuem ao conteúdo versionado do commit. Alterar o conteúdo do ZIP exige novos hashes e nova verificação.

O responsável já configurou o segredo e autorizou commit/push na branch do PR. Uma eventual integração à `main` depende de autorização específica; se realizada, deve receber CI do SHA integrado. G-02/G-03 permanecem limitações históricas, sem transformar a conclusão atual de E4.2 em declaração de 248 requisitos concluídos.

Nenhum ZIP foi publicado em serviço externo. As execuções históricas permanecem identificadas em seus diretórios; a revisão de 06/11 reconfirmou os runs original, negativo, recuperação e CI final remoto. O ensaio negativo continua válido para a ordem dos gates preservada. Os oito marcos anteriores de G-02/G-03 não foram fabricados.
