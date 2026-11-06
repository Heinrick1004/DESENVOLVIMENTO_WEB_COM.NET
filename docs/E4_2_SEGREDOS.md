# R8-17 / E4.2 — cofre de segredos do provedor

Fonte: `Roteiros.pdf`, página 25: “Usar identidade federada ou secret store do provedor, evitando credenciais longas quando possível.”

## Decisão técnica

O destino de teste existente é o runner efêmero do GitHub Actions com SQL Server e aplicação Docker/HTTPS. A alternativa escolhida é **GitHub Actions Secrets**, sem criar provedor de nuvem ou infraestrutura adicional. A senha do PFX de teste vem do segredo de repositório `PORTAL_CI_CERTIFICATE_PASSWORD`. O certificado e sua chave são criados para a execução, montados somente para leitura no contêiner e removidos no encerramento. O segredo só aparece no ambiente da etapa que o consome e, após mascaramento, na variável necessária ao contêiner. Não é incorporado à imagem ou aos artefatos.

A senha SQL continua aleatória por job. O `GITHUB_TOKEN` mantém apenas `contents: read`; o checkout não persiste sua credencial. Não se utiliza PAT, credencial de nuvem ou `id-token: write`. OIDC só seria necessário com um destino compatível. GitHub Environments e aprovação de promoção para produção são alternativas úteis se existir esse destino; não são exigidos para o runner de teste atual.

O uso de Actions Secrets como cofre é uma interpretação técnica do requisito, sustentada pela documentação oficial do GitHub: [armazenamento e injeção de secrets](https://docs.github.com/en/actions/concepts/security/secrets), [uso de secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets) e [token temporário do job](https://docs.github.com/en/actions/concepts/security/github_token). A avaliação acadêmica final cabe ao avaliador. A existência de variáveis de ambiente ou a senha SQL aleatória, isoladamente, não comprova E4.2.

## Configuração indispensável na conta

1. Um administrador deve abrir o repositório no GitHub: **Settings → Secrets and variables → Actions → New repository secret**.
2. Nome: `PORTAL_CI_CERTIFICATE_PASSWORD`. Valor: senha aleatória exclusiva para esse teste, com pelo menos 16 caracteres, em uma única linha. Não usar uma senha pessoal, de banco ou de produção. Não enviar o valor por chat nem versioná-lo.
3. Salvar o segredo. Se for necessária rotação, atualizar o mesmo nome; a próxima execução emitirá outro certificado. O valor não pode ser recuperado pela interface depois de salvo.
4. Após autorização de envio, publicar as alterações da branch do PR e conferir a nova execução. Não é necessário integrar na `main` para validar o PR.
5. PRs de forks e execuções do Dependabot não recebem esse secret. A etapa falhará explicitamente se ele estiver ausente/inválido; não há senha alternativa nem aprovação silenciosa. Revisar e executar em branch confiável do próprio repositório, sem mudar para `pull_request_target`.

## Critério de comprovação

Conferir na **mesma execução e commit**: geração do PFX com o Actions Secret; inicialização HTTPS da imagem usando esse PFX; smoke aprovado; estado Docker `healthy`; save/upload após esses gates. Preservar metadados sanitizados do run/jobs e manifesto de smoke/saúde, sem o PFX, senha, cookies ou tokens. A abertura HTTPS comprova que o contêiner conseguiu abrir o certificado com a senha recebida.

Estado em 06/11/2026: **implementado sem execução remota da alteração**. O secret ainda depende de configuração/confirmação do administrador e o envio depende de autorização explícita. A validação local com uma senha externa de teste verifica a lógica, mas não comprova leitura do cofre GitHub. O CI anterior continua sendo evidência histórica de seu próprio commit. O ensaio negativo anterior permanece válido para a ordem dos gates, que foi preservada.

Atualização antes do envio: o responsável confirmou que criou o segredo e autorizou commit/push na branch do PR. O valor não foi solicitado nem lido. A comprovação continua dependendo do novo run; autorização de merge permanece separada.
