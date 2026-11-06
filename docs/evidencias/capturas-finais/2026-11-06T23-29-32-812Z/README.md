# Capturas finais reais

Resultado: **aprovado**. UTC: 2026-11-06T23:29:33.298Z. Commit base: `5bf8fefcdd2b7b02dfb7012f8663e584a530b79e`.

Versão servida: `{"version":"5bf8fefcdd2b7b02dfb7012f8663e584a530b79e"}`. Alterações locais e hashes das imagens estão em [manifest.json](manifest.json).

Aplicação real publicada com SQL Server exclusivo e conta sintética. Nenhum corpo HTTP, DOM ou estado React foi fabricado.

TLS local ignorado exclusivamente no navegador de captura. Idle pausado via CDP; loading e erro usam controle de rede declarado. Imagens anteriores preservadas.

| Roteiro | Imagem | Procedimento |
|---|---|---|
| 2 | [r2-01-lista-mvc.png](r2-01-lista-mvc.png) | Lista real autenticada. |
| 2 | [r2-02-detalhes-mvc.png](r2-02-detalhes-mvc.png) | Detalhes reais da tarefa do seed. |
| 2 | [r2-03-cadastro-mvc.png](r2-03-cadastro-mvc.png) | Formulário de cadastro real. |
| 2 | [r2-04-validacao-mvc.png](r2-04-validacao-mvc.png) | POST real inválido; valores preservados e nenhuma gravação. |
| 4 | [r4-01-swagger-operacoes.png](r4-01-swagger-operacoes.png) | Swagger UI servido em Development, operações reais. |
| 4 | [r4-02-swagger-contrato.png](r4-02-swagger-contrato.png) | Captura do bloco real da operação expandida, após carregar parâmetros e respostas do contrato. |
| 5 | [r5-04-success.png](r5-04-success.png) | Dados fictícios reais do SQL de teste, sem interceptação. |
| 5 | [r5-03-empty.png](r5-03-empty.png) | Filtro único sem resultados, resposta real do servidor. |
| 5 | [r5-02-loading.png](r5-02-loading.png) | Controle de rede: requisição real temporariamente retida antes de seguir ao servidor; nenhum corpo substituído. |
| 5 | [r5-05-error-rede.png](r5-05-error-rede.png) | Controle de rede: uma consulta GET abortada no navegador. Corpo de erro não fabricado. |
| 5 | [r5-06-problem-details.png](r5-06-problem-details.png) | POST real: prioridade alta sem prazo, HTTP 400 e mensagens do servidor. |
| 5 | [r5-01-idle.png](r5-01-idle.png) | Quadro real pausado via CDP/MutationObserver antes do useEffect; DOM/estado React preservados. |

As credenciais, cookies, tokens, connection strings e o arquivo de conta não são copiados. Os cadastros inválidos usam SQL de teste e não gravam tarefas.
