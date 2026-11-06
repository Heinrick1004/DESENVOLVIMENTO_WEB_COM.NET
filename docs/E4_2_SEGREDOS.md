# R8-17 / E4.2 — cofre de segredos do provedor

Fonte: `Roteiros.pdf`, página 25: “Usar identidade federada ou secret store do provedor, evitando credenciais longas quando possível.”

## Decisão técnica

O destino de teste existente é o runner efêmero do GitHub Actions com SQL Server e aplicação Docker/HTTPS. A alternativa escolhida é **GitHub Actions Secrets**, sem criar provedor de nuvem ou infraestrutura adicional. A senha do PFX de teste vem do segredo de repositório `PORTAL_CI_CERTIFICATE_PASSWORD`. O certificado e sua chave são criados para a execução, montados somente para leitura no contêiner e removidos no encerramento. O segredo só aparece no ambiente da etapa que o consome e, após mascaramento, na variável necessária ao contêiner. Não é incorporado à imagem ou aos artefatos.

A senha SQL continua aleatória por job. O `GITHUB_TOKEN` mantém apenas `contents: read`; o checkout não persiste sua credencial. Não se utiliza PAT, credencial de nuvem ou `id-token: write`. OIDC só seria necessário com um destino compatível. GitHub Environments e aprovação de promoção para produção são alternativas úteis se existir esse destino; não são exigidos para o runner de teste atual.

O uso de Actions Secrets como cofre é uma interpretação técnica do requisito, sustentada pela documentação oficial do GitHub: [armazenamento e injeção de secrets](https://docs.github.com/en/actions/concepts/security/secrets), [uso de secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets) e [token temporário do job](https://docs.github.com/en/actions/concepts/security/github_token). A avaliação acadêmica final cabe ao avaliador. A existência de variáveis de ambiente ou a senha SQL aleatória, isoladamente, não comprova E4.2.

## Configuração e rotação na conta

1. Um administrador deve abrir o repositório no GitHub: **Settings → Secrets and variables → Actions → New repository secret**.
2. Nome: `PORTAL_CI_CERTIFICATE_PASSWORD`. Valor: senha aleatória exclusiva para esse teste, com pelo menos 16 caracteres, em uma única linha. Não usar uma senha pessoal, de banco ou de produção. Não enviar o valor por chat nem versioná-lo.
3. Salvar o segredo. Se for necessária rotação, atualizar o mesmo nome; a próxima execução emitirá outro certificado. O valor não pode ser recuperado pela interface depois de salvo.
4. O responsável confirmou a configuração do segredo e autorizou commit/push da branch. Para futuras alterações, conferir o CI do novo SHA; a integração na `main` exige autorização própria.
5. PRs de forks e execuções do Dependabot não recebem esse secret. A etapa falhará explicitamente se ele estiver ausente/inválido; não há senha alternativa nem aprovação silenciosa. Revisar e executar em branch confiável do próprio repositório, sem mudar para `pull_request_target`.

## Critério de comprovação

Conferir na **mesma execução e commit**: geração do PFX com o Actions Secret; inicialização HTTPS da imagem usando esse PFX; smoke aprovado; estado Docker `healthy`; save/upload após esses gates. Preservar metadados sanitizados do run/jobs e manifesto de smoke/saúde, sem o PFX, senha, cookies ou tokens. A abertura HTTPS comprova que o contêiner conseguiu abrir o certificado com a senha recebida.

## Resultado comprovado — 06/11/2026

**R8-17: concluído e verificado.** O [CI 42313806838, tentativa 2](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42313806838/attempts/2) terminou aprovado para o head `90ad9a2f6241a520df38d3be9b77a60fc76df9db`, testado no merge sintético `731aadb3d531e22820f086f472f7ddad8b5151db`. Foram aprovados 55 testes .NET e 33 React, zero falhos e zero ignorados, além de SQL/migrations, contrato/cliente, builds, Docker com HTTPS e usuário não privilegiado, smoke, saúde nativa e save/upload. A etapa sem fallback consumiu o Actions Secret para gerar o PFX; o contêiner abriu esse certificado e respondeu por HTTPS, com manifesto nativo `healthy`. O agente não consultou o valor do segredo; ele não foi incluído nas evidências, imagem ou pacote. A [comprovação sanitizada](evidencias/e4-2-20261106/github/comprovacao-e4-2.json) identifica o head, checkout, árvore de fontes, testes, etapas e os manifestos extraídos do artefato remoto, sem recuperar o valor do segredo.

A [tentativa 1](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42313806838/attempts/1) foi reprovada pela guarda que exige ao menos 16 caracteres no Actions Secret, antes de executar o contêiner, smoke, saúde nativa e entrega. Os testes anteriores da execução haviam passado; essa rejeição foi de configuração. O responsável informou a correção do segredo e a tentativa 2 reexecutou o mesmo head/checkout. Os [registros da primeira tentativa](evidencias/e4-2-20261106/github/comprovacao-e4-2-attempt1.json) permanecem preservados; não são substituídos pelo resultado aprovado posterior.

Antes dessa execução, o requisito estava implementado sem comprovação remota. A validação com stubs permanece evidência somente da lógica local; a mudança de estado decorre deste run real. A senha SQL aleatória e variáveis externas, isoladamente, não foram usadas para encerrar o item. O ensaio negativo anterior continua válido porque a ordem dos gates de publicação foi preservada.

A aprovação remota da revisão efetivamente empacotada deve ser conferida nos checks do PR e no comprovante final associado ao SHA registrado no manifesto do pacote. A execução citada comprova seus próprios head e checkout; não aprova automaticamente commits posteriores. O comprovante do CI final e os dados de integridade do ZIP ficam nos arquivos de acompanhamento ao lado do pacote, sem alterar novamente as fontes versionadas. O mecanismo escolhido atende à alternativa de secret store no ambiente de teste adotado, sem acrescentar nuvem, registry ou implantação pública. As limitações históricas gerais G-02/G-03 permanecem na matriz; elas não são falha do mecanismo de segredos.
