# Capturas finais — 06/11/2026

A coleta **definitiva** é [2026-11-06T23-29-32-812Z](2026-11-06T23-29-32-812Z/README.md): **12 imagens**, quatro MVC, duas Swagger e seis React. Todas foram inspecionadas visualmente, com conteúdo carregado, erros legíveis e dados fictícios. O [manifesto](2026-11-06T23-29-32-812Z/manifest.json) identifica a versão servida, as condições dos cenários, os status HTTP, a ausência de gravação dos cadastros inválidos e os hashes das imagens.

O binário responde com a versão `5bf8fefcdd2b7b02dfb7012f8663e584a530b79e`. As mudanças locais desta finalização abrangem CI, scripts e documentação, sem alterar o código backend/frontend. Isso não representa CI remoto aprovado para as mudanças locais ainda não enviadas.

O SQL Server e a sessão são reais e exclusivos de teste. Success, empty e Problem Details usam respostas reais. Loading atrasa uma requisição real, error interrompe uma consulta no navegador e idle pausa um quadro transitório via CDP. Não foram fabricados corpos HTTP, DOM ou estado React. A exceção de TLS fica restrita ao contexto local de captura; os clientes Node da homologação validaram TLS separadamente.

## Tentativas diagnósticas preservadas

- [2026-11-06T23-28-02-600Z](2026-11-06T23-28-02-600Z/manifest-diagnostico.json): nove imagens antes de uma condição de corrida do script de captura ao liberar uma requisição retida. O problema foi corrigido somente no script. A execução não completou o manifesto nem confirmou o logout. Não integra a contagem final.
- [2026-11-06T23-28-33-857Z](2026-11-06T23-28-33-857Z/REVISAO_VISUAL.md): as 12 imagens foram coletadas e os status passaram, mas a inspeção encontrou o bloco Swagger ainda com spinner. A coleta definitiva acrescentou espera explícita por parâmetros e respostas e capturou diretamente o bloco carregado. Não integra a contagem final.

As imagens diagnósticas e as evidências de outubro não foram apagadas, editadas ou sobrescritas. Os relatórios 02, 04 e 05 incorporam a pasta definitiva.
