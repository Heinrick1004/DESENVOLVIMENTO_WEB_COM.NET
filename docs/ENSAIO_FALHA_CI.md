# Ensaio controlado de bloqueio da publicação

## Estado em 05/11/2026

**Ensaio remoto pendente de autorização.** A preparação e a falha local não comprovam o comportamento do GitHub Actions. O requisito R8-15 (p.25, V3 de `Roteiros.pdf`) continua pendente até existir uma execução remota reprovada com as etapas de publicação ignoradas.

O CI aprovado da `main`, [execução 42117773881](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42117773881), comprova o percurso de sucesso. O job executa testes .NET, testes frontend, contrato, typecheck e build antes de `dotnet publish`, `docker build`, `docker save` e upload do artefato de entrega. Essas etapas usam a condição padrão de sucesso, sem `continue-on-error`. As etapas de diagnóstico com `if: failure()` podem publicar logs após uma falha; esses logs não são o artefato de entrega.

## Preparação isolada e recuperação local

Foi criada a worktree `.local/ensaio-ci-falha-20261105`, ignorada pela cópia principal, e a branch `experimento/ci-bloqueio-20261105`, a partir do commit aprovado `1da2eead374d03fcfb62e5b0d7af17e2dbaf1cb2`. Nenhuma falha foi inserida na `main` ou na branch de correções `auditoria/final-20261105`.

| Commit local | Alteração |
|---|---|
| `6779ffc2bab6ed0732f8e90f0959d834a73fa25b` | Acrescenta somente o nome exato da branch experimental ao gatilho de push do workflow |
| `1423c195e52ad2aaa75fdcfab562ba53456899dc` | Acrescenta um único `[Fact]` com `Assert.Fail`, sem banco, credencial ou dado real |
| `943e944a13d39c08bc82b9bb314c13986f5674b3` | Reverte integralmente o teste intencional; mantém o gatilho isolado para a execução de recuperação |

O teste intencional compilou e retornou código 1: **0 aprovados, 1 reprovado, 0 ignorados**. Após a reversão, os testes de domínio/memória passaram: **29 aprovados, 0 reprovados, 0 ignorados**. TRX e [resumo](evidencias/auditoria-final-20261105/ensaio-local/resumo.json) ficam em `docs/evidencias/auditoria-final-20261105/ensaio-local/`. Esses resultados são deliberadamente separados da regressão normal, para que a falha esperada não seja confundida com defeito do produto.

A branch experimental local termina na reversão, sem o teste reprovado. Seu único diff em relação à base é o gatilho específico do workflow. Nenhum commit experimental deve ser mesclado na `main`.

## Execução remota após autorização expressa

Os comandos a seguir estão preparados; sua presença neste documento **não autoriza nem comprova execução remota**. Executar a partir da pasta `PortalTarefas` e conferir antes se o remoto ainda não possui a branch experimental. Se ela já existir ou tiver outro SHA, interromper e inspecionar; não usar force push.

1. Enviar especificamente o commit que contém a falha, sem enviar ainda a reversão:

   ```powershell
   git push origin 1423c195e52ad2aaa75fdcfab562ba53456899dc:refs/heads/experimento/ci-bloqueio-20261105
   ```

2. Consultar o GitHub Actions para a branch e conferir o `head_sha` completo. A execução esperada deve reprovar em **Aplicar migrations e executar testes sem ignorados**, com o nome `CiPublicationGateTests.FalhaControladaDeveInterromperPublicacao` no log/TRX. Uma falha de infraestrutura anterior não aprova este ensaio.

3. Registrar URL, ID, data, commit e conclusão da execução; obter os estados de todas as etapas e metadados dos artefatos. Confirmar `skipped` para publicação em pasta, build da imagem, execução/smoke da imagem, `docker save` e upload de entrega. Confirmar ausência de `portaltarefas-1423c195e52ad2aaa75fdcfab562ba53456899dc`. Um artefato `diagnostico-<SHA>` é permitido e deve ser identificado como diagnóstico.

4. Somente depois da execução negativa terminar e ser registrada, enviar a recuperação já preparada, por avanço normal:

   ```powershell
   git push origin 943e944a13d39c08bc82b9bb314c13986f5674b3:refs/heads/experimento/ci-bloqueio-20261105
   ```

5. Confirmar a nova execução aprovada, o SHA da reversão e a ausência do teste intencional. Registrar separadamente eventual impedimento de infraestrutura. Conferir que `origin/main` continua no SHA aprovado original até que uma atualização própria seja autorizada.

O workflow experimental conserva os mesmos testes obrigatórios e a mesma ordem do pipeline aprovado. Ele não contém publicação em GHCR nem implantação em destino externo. A recuperação pode produzir o artefato temporário normal do CI, sujeito à retenção de sete dias. A exclusão da branch remota, se desejada, precisa estar abrangida pela autorização; não é necessária para a validade da evidência.

## Limites e preservação

- A falha é restrita a um teste sintético e a commits reais de uma branch isolada.
- Banco e contêineres do runner são efêmeros e específicos da execução; não se usa banco compartilhado.
- Os arquivos de configuração, regras de negócio e testes normais permanecem preservados.
- Nenhum push, dispatch, alteração de infraestrutura ou captura de tela é realizado apenas pela preparação local.
- Só marcar R8-15 como concluído após observar a falha esperada e a ausência das etapas/artefatos de publicação daquele SHA.
