# Ensaio controlado de bloqueio da publicação

## Estado em 05/11/2026

**Bloqueio remoto comprovado, requisito R8-15/V3 concluído.** Depois de autorização explícita, o [run 42122062762](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122062762) reprovou pelo teste sintético e não executou as etapas de publicação. A conclusão `failure` é o resultado esperado do ensaio, separado das regressões normais aprovadas. A [recuperação 42122508347](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122508347) também foi aprovada após remover a falha. O [CI do PR 42122467632](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122467632) comprovou separadamente o novo gate nativo Docker.

O CI aprovado da `main`, [execução 42117773881](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42117773881), comprova o percurso de sucesso da base. O job executa testes .NET, testes frontend, contrato, typecheck e build antes de `dotnet publish`, `docker build`, `docker save` e upload do artefato de entrega. Essas etapas usam a condição padrão de sucesso, sem `continue-on-error`. O diagnóstico com `if: failure()` pode preservar logs depois de uma falha; esse diagnóstico não publica a aplicação.

## Preparação isolada e recuperação local

Foi criada a worktree `.local/ensaio-ci-falha-20261105`, ignorada pela cópia principal, e a branch `experimento/ci-bloqueio-20261105`, a partir do commit aprovado `1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`. Nenhuma falha foi inserida na `main` ou na branch de correções `auditoria/final-20261105`.

| Commit real | Alteração |
|---|---|
| `6779ffc2bab6ed0732f8e90f0959d834a73fa25b` | Acrescenta somente o nome exato da branch experimental ao gatilho de push do workflow |
| `1423c195e52ad2aaa75fdcfab562ba53456899dc` | Acrescenta um único `[Fact]` com `Assert.Fail`, sem banco, credencial ou dado real |
| `943e944a13d39c08bc82b9bb314c13986f5674b3` | Reverte integralmente o teste intencional; mantém o gatilho isolado para a execução de recuperação |

O teste intencional local compilou e retornou código 1: **0 aprovados, 1 reprovado, 0 ignorados**. Após a reversão local, os testes de domínio/memória passaram: **29 aprovados, 0 reprovados, 0 ignorados**. TRX e [resumo](evidencias/auditoria-final-20261105/ensaio-local/resumo.json) permanecem identificados como resultados locais. Esses 29 testes são um subconjunto da suíte normal; não somar resultados de execuções diferentes como casos novos.

A branch experimental termina na reversão, sem o teste reprovado. Seu único diff em relação à base é o gatilho específico do workflow. Nenhum commit experimental deve ser mesclado na `main`.

## Execução negativa remota autorizada

A [autorização de envio](evidencias/auditoria-final-20261105/github/autorizacao-envios.json) abrangeu enviar o commit intencional, observar sua falha e somente depois enviar a reversão. O [run 42122062762](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122062762) executou por `push` em `experimento/ci-bloqueio-20261105`, SHA **`1423c195e52ad2aaa75fdcfab562ba53456899dc`**, entre 22:30:28 e 22:31:57 UTC (19:30:28–19:31:57 de São Paulo), em 05/11/2026. Estado `completed`, conclusão `failure`.

Restore/build, SQL Server e migrations anteriores passaram. A etapa **Aplicar migrations e executar testes sem ignorados** retornou código 1 por `PortalTarefas.Tests.CiPublicationGateTests.FalhaControladaDeveInterromperPublicacao`. O total foi **56 testes: 55 aprovados, uma reprovação intencional e zero ignorados**. Nenhuma falha de infraestrutura foi usada como substituto da falha de teste exigida.

| Etapa posterior | Resultado observado |
|---|---|
| Publicar aplicação em pasta | `skipped` |
| Criar imagem identificada pelo commit | `skipped` |
| Executar a imagem real com SQL Server e HTTPS | `skipped` |
| Validar Health Checks, MVC, SPA e versão da imagem | `skipped` |
| Salvar imagem somente após o smoke test aprovado | `skipped` |
| Preservar artefatos após testes e smoke aprovados | `skipped` |

As etapas frontend também ficaram `skipped` após a interrupção .NET; isso significa que não foram executadas, não uma aprovação nem testes ignorados dentro do Vitest. A API informou **ausência de `portaltarefas-1423c195e52ad2aaa75fdcfab562ba53456899dc`**. O único artefato foi `diagnostico-1423c195e52ad2aaa75fdcfab562ba53456899dc`, ID `13458922164`, 66.567 bytes, digest `sha256:69e5a1cdb7abe4f0708248b264a51c7eb60cd66611dd8e69d8670ee4585f6f6e`, com expiração registrada em 12/11/2026 às 22:31:44 UTC. Esse ZIP contém diagnóstico de falha, sem artefato de entrega.

[Resumo da execução](evidencias/auditoria-final-20261105/github/ensaio-negativo-execucao.json), [jobs/etapas](evidencias/auditoria-final-20261105/github/ensaio-negativo-jobs.json), [artefatos](evidencias/auditoria-final-20261105/github/ensaio-negativo-artefatos.json) e [trechos de resultado do log](evidencias/auditoria-final-20261105/github/ensaio-negativo-trechos-log.json) comprovam a conclusão. Logs brutos com valores de ambiente não foram versionados.

## Recuperação remota aprovada

Após registrar o resultado negativo, a reversão **`943e944a13d39c08bc82b9bb314c13986f5674b3`** foi enviada por avanço normal, sem force push. A [execução de recuperação 42122508347](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122508347) terminou `completed/success` em 05/11/2026, 22:33:37–22:36:36 UTC (19:33:37–19:36:36 de São Paulo). O resultado foi **55 testes .NET e 33 React aprovados, zero reprovados e zero ignorados**, com restore/build, SQL/migrations, contrato/cliente, typecheck/build, Docker/SQL/HTTPS, usuário não privilegiado, smoke e save/upload aprovados.

O [artefato 13460037445](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122508347/artifacts/13460037445) chama-se `portaltarefas-943e944a13d39c08bc82b9bb314c13986f5674b3`, tem 159.295.700 bytes, digest `sha256:bd8d384b0d5b0f7caaf386c4ce3206714e23db586c093af8e32429adeeb286ff` e expiração em 12/11/2026 às 22:36:06 UTC. [Resumo](evidencias/auditoria-final-20261105/github/ensaio-recuperacao-execucao.json), [jobs/etapas](evidencias/auditoria-final-20261105/github/ensaio-recuperacao-jobs.json), [artefatos](evidencias/auditoria-final-20261105/github/ensaio-recuperacao-artefatos.json) e [log](evidencias/auditoria-final-20261105/github/ensaio-recuperacao-trechos-log.json) registram o retorno do pipeline completo. Os metadados e o upload foram conferidos; o ZIP não foi extraído.

## CI do novo gate nativo aprovado separadamente

A criação do [draft PR 1](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/pull/1) foi [autorizada](evidencias/auditoria-final-20261105/github/autorizacao-pr.json). Na execução analisada, o head de `auditoria/final-20261105` era **`bfc46abbf99737724055af555bd785939bb795b8`**, com base `1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`. O [run do PR 42122467632](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122467632), evento `pull_request`, terminou `completed/success` em 05/11/2026, 22:33:20–22:36:40 UTC (19:33:20–19:36:40 de São Paulo).

O commit testado, correspondente a `github.sha` e à identificação da imagem/artefato, foi o **merge sintético `3f4c18a0c47ba70d3a8190f4cc0437257d5d987b`**. Ele difere do SHA do head do PR. Foram aprovados 55 testes .NET e 33 React, sem falhos/ignorados, smoke e a etapa 27 de saúde nativa; save e upload vieram nas etapas 28 e 29. O estado healthy é uma inferência declarada pelo sucesso da etapa: seu script só retorna zero quando inspect responde healthy, rejeitando unhealthy ou timeout. O valor de inspect não foi impresso e o manifesto interno não foi extraído do ZIP.

O [artefato 13458418239](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42122467632/artifacts/13458418239), `portaltarefas-3f4c18a0c47ba70d3a8190f4cc0437257d5d987b`, possui 159.298.190 bytes, digest `sha256:c8d75257563beda8cc831f91e764cf9c107e156312897fd9aae8a118461a369f` e expiração em 12/11/2026 às 22:36:11 UTC. [Execução/SHAs](evidencias/auditoria-final-20261105/github/ci-saude-nativa-execucao.json), [jobs/etapas](evidencias/auditoria-final-20261105/github/ci-saude-nativa-jobs.json), [artefatos](evidencias/auditoria-final-20261105/github/ci-saude-nativa-artefatos.json) e [resultados/limites](evidencias/auditoria-final-20261105/github/ci-saude-nativa-trechos-log.json) preservam a comprovação.

O pipeline experimental não inclui esse novo gate e não serve como sua prova. A `main` permanece no commit `1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`, sem teste intencional e sem merge do PR. Não houve publicação em GHCR, implantação pública, exclusão de branch remota ou alteração de infraestrutura externa.

## Comandos históricos de reprodução

Estes são os comandos usados no ensaio autorizado, registrados para reprodução. Sua presença no documento não autoriza novos envios. Antes de repetir uma alteração remota, conferir branch e SHA atuais e obter a autorização correspondente; não usar force push para substituir um ensaio existente.

```powershell
git push origin 1423c195e52ad2aaa75fdcfab562ba53456899dc:refs/heads/experimento/ci-bloqueio-20261105
# Após concluir e registrar a execução negativa:
git push origin 943e944a13d39c08bc82b9bb314c13986f5674b3:refs/heads/experimento/ci-bloqueio-20261105
```

## Limites e preservação

- A falha foi restrita a um teste sintético e a commits reais de uma branch isolada.
- Bancos e contêineres do runner são efêmeros e específicos da execução; não se usa banco compartilhado.
- Configuração, regras de negócio e testes normais permanecem preservados; o resultado negativo não representa defeito do produto.
- R8-15 foi concluído pela falha esperada, estados das etapas e ausência do artefato publicável observados. A recuperação e o gate nativo têm suas próprias execuções aprovadas.
- A exclusão da branch remota depende de autorização própria e não é necessária para validar a evidência.
- Não houve novas capturas de tela ou alteração dos relatórios 1–7 e imagens existentes. E4.2, identidade federada/secret store do provedor, continua pendente.
