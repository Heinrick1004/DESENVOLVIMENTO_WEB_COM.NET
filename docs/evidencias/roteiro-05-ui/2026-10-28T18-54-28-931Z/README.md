# Evidências de interface — Roteiro 5

Capturas reais do React publicado com HTTP interceptado no navegador para simulação didática.

Não comprova SQL Server, autenticação ou autorização reais. O certificado local é ignorado apenas neste contexto de captura.

URL: https://localhost:7240/app/

Ambiente informado pelo operador: Development

Instante UTC: 2026-10-28T18:54:29.531Z

Navegador: 156.0.8078.49; Playwright Core 1.64.1.

| Estado | Resultado | Arquivo ou motivo |
|---|---|---|
| idle | capturado | [01-idle.png](01-idle.png) |
| loading | capturado | [02-loading.png](02-loading.png) |
| empty | capturado | [03-empty.png](03-empty.png) |
| success | capturado | [04-success.png](04-success.png) |
| error-network | capturado | [05-error-network.png](05-error-network.png) |
| problem-details | capturado | [06-problem-details.png](06-problem-details.png) |

Os dados de sucesso, a sessão do formulário, o atraso, a falha de rede e o Problem Details são simulações controladas por interceptação HTTP. O arquivo manifest.json registra o procedimento e as rotas interceptadas. O estado idle é um quadro transitório real capturado com a execução pausada antes do useEffect, quando observado; o DOM e o estado React não foram editados. Nenhuma operação de escrita chega ao servidor.

Para repetir, instale Playwright Core 1.64.1 em .tools/ui-evidence e execute scripts/Capture-UiEvidence.mjs com Node.js e --url apontando para a SPA publicada.
