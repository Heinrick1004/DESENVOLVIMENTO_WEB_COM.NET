# Capturas finais — execução de 06/11/2026

As capturas foram realizadas **depois** da aprovação dos testes .NET/React, build, contrato real e homologação HTTPS. Foram geradas 12 imagens da aplicação publicada sobre SQL Server exclusivo, com dados fictícios e conta sintética. Não há edição de DOM/imagens nem resposta HTTP fabricada. As evidências anteriores permanecem preservadas.

[Manifesto com versão, ambiente, banco, navegador, verificações e hashes](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/manifest.json). O SHA de origem é `5bf8fefcdd2b7b02dfb7012f8663e584a530b79e`; o manifesto registra as alterações locais ainda não commitadas. A fonte funcional da aplicação foi preservada; CI/documentação/scripts posteriores não são apresentados como já incluídos naquele commit.

| ID | Roteiro / fonte | Imagem existente | Cenário |
|---|---|---|---|
| CF-01 | R2, p.8 | [r2-01-lista-mvc.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r2-01-lista-mvc.png) | Lista MVC real |
| CF-02 | R2, p.8 | [r2-02-detalhes-mvc.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r2-02-detalhes-mvc.png) | Detalhes MVC reais |
| CF-03 | R2, p.8 | [r2-04-validacao-mvc.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r2-04-validacao-mvc.png) | Validação POST real; entrada preservada e sem gravação |
| CF-04 | R4, p.14 | [r4-01-swagger-operacoes.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r4-01-swagger-operacoes.png) | Swagger real em Development |
| CF-05 | R5, p.17 | [r5-01-idle.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r5-01-idle.png) | Quadro transitório pausado via CDP antes do efeito |
| CF-06 | R5, p.17 | [r5-02-loading.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r5-02-loading.png) | Rede controlada: atraso de GET real |
| CF-07 | R5, p.17 | [r5-03-empty.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r5-03-empty.png) | Filtro sem resultado: HTTP real |
| CF-08 | R5, p.17 | [r5-04-success.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r5-04-success.png) | Dados reais fictícios do SQL |
| CF-09 | R5, p.17 | [r5-05-error-rede.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r5-05-error-rede.png) | Rede controlada: GET abortado; recuperação verificada |
| CF-10 | R5, p.17 | [r5-06-problem-details.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r5-06-problem-details.png) | POST real 400 por prioridade alta sem prazo |
| CF-11 | R2, p.8 | [r2-03-cadastro-mvc.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r2-03-cadastro-mvc.png) | Formulário de cadastro real |
| CF-12 | R4, p.14 | [r4-02-swagger-contrato.png](evidencias/capturas-finais/2026-11-06T23-29-32-812Z/r4-02-swagger-contrato.png) | Operação e contrato expandido |

Os 12 itens estão executados; nenhuma captura final obrigatória permanece pendente. A exceção de confiança do certificado local está restrita ao contexto do navegador. Credenciais, cookies e tokens não são gravados nas evidências. Swagger/OpenAPI continuam restritos a Development; Production foi verificada com 404.

## Reprodução

Instalar Chrome/Edge e Playwright Core como ferramenta separada, sem alterar as dependências do produto:

```powershell
npm.cmd install --prefix .tools/ui-evidence --ignore-scripts --save-exact playwright-core@1.64.1
node scripts/Capture-FinalEvidence.mjs --url https://localhost:8340 --credentials .local/capture-user.json --database PortalTarefas_Tests_CapturasNovaExecucao --environment Development
```

O servidor precisa estar iniciado e validado, com build React integrado, contas sintéticas e banco exclusivo criado conforme README. O arquivo privado de conta usa `email` e `password` e deve ficar em `.local/`, nunca em `docs/`. Não utilizar dados pessoais. Cada execução cria outra pasta datada, sem sobrescrever as imagens anteriores. Verificar `resultado=aprovado`, 12 imagens e logout confirmado no manifesto; uma execução incompleta não conclui o checklist.

Os registros do roteiro 8 são textuais: CI/Docker/health/ensaio negativo não exigem prints adicionais. A reprodução Docker local continua bloqueada por ausência de Engine; E4.2 foi comprovado no [CI 42313806838, tentativa 2](https://github.com/Heinrick1004/DESENVOLVIMENTO_WEB_COM.NET/actions/runs/42313806838/attempts/2) com Actions Secrets, HTTPS/smoke e saúde nativa, conforme [status](STATUS_DE_EXECUCAO.md). A primeira tentativa reprovada permanece no histórico. Os limites do ambiente local e as limitações históricas permanecem explícitos.
