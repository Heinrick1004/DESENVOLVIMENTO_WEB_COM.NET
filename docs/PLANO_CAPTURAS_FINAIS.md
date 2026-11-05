# Plano de capturas finais — Portal de Tarefas

Esta é somente a lista de procedimentos para a etapa visual posterior. Nenhuma captura será executada na finalização técnica do roteiro 8. As imagens existentes permanecem preservadas, com suas datas, limites e referências originais; não são apresentadas como novas capturas.

O PDF exige capturas ou transcrições no roteiro 2, evidência da UI OpenAPI quando adotada no roteiro 4 e capturas dos estados React/Problem Details no roteiro 5. Os registros de build, logs, métricas, traces, CI e contêiner do roteiro 8 podem ser textuais: não se cria uma obrigação de screenshot onde o documento aceita registro de execução.

## Preparação da etapa posterior

- [ ] Estabilizar a versão a ser entregue e registrar commit, ambiente, URL, data e objetivo de cada nova imagem.
- [ ] Iniciar a aplicação conforme o README, com configuração externa e banco exclusivo de teste; usar contas sintéticas sem expor senha, cookie, token ou conexão nas telas.
- [ ] Confirmar `/health/live` e `/health/ready` e a versão da aplicação antes do percurso visual.
- [ ] Guardar futuras capturas em uma pasta nova, preservando os arquivos históricos. Não sobrescrever nem editar as imagens existentes.
- [ ] Identificar explicitamente cenários de rede controlados. Não editar DOM, fabricar dados exibidos ou substituir imagens para representar um resultado que não ocorreu.

## Checklist das telas acadêmicas

| ID | Fonte | Captura posterior | Procedimento e resultado esperado | Evidência anterior preservada |
|---|---|---|---|---|
| CF-01 | R2, p.8 | Lista MVC | Abrir `/Tarefas/Index`; demonstrar tarefas visíveis ao perfil selecionado e navegação compartilhada. Registrar perfil sem credenciais. | [Lista MVC](evidencias/roteiro-02-mvc/01-lista.png) |
| CF-02 | R2, p.8 | Detalhes MVC | A partir da lista, abrir uma tarefa existente. Conferir título, descrição, situação e links de navegação. | [Detalhes MVC](evidencias/roteiro-02-mvc/02-detalhes.png) |
| CF-03 | R2, p.8 | Formulário MVC inválido | Autenticar com conta de teste, abrir cadastro e enviar título curto com descrição identificável. Conferir erro em português, valores preservados e ausência de gravação. Repetir com título válido, prioridade alta e prazo vazio para demonstrar a validação cruzada, se essa regra for incluída na imagem final. | [Formulário corrigido](evidencias/roteiro-02-mvc/03-formulario-invalido.png); [manifesto](evidencias/roteiro-02-mvc/manifest.json) |
| CF-04 | R4, p.14 | Swagger UI em Development | Abrir `/swagger/index.html`, conferir operações, parâmetros, exemplos e esquemas do contrato gerado. Não inserir segredos na imagem. A documentação deve continuar indisponível em Production. | [Swagger real](evidencias/roteiro-04-openapi.png); [metadados](evidencias/roteiro-04-openapi.json) |
| CF-05 | R5, p.17 | React: idle | Abrir a SPA em `/app/` e observar o estado inicial anterior à consulta. Por ser transitório, pode-se pausar a execução antes do efeito de consulta pelas ferramentas do navegador, sem editar o DOM ou o estado da aplicação. Registrar o método usado. | [Idle](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/01-idle.png) |
| CF-06 | R5, p.17 | React: loading | Aplicar limitação de rede pelas ferramentas do navegador e atualizar a lista. Capturar o feedback de carregamento enquanto a requisição real estiver pendente. Restaurar a rede ao terminar. | [Loading](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/02-loading.png) |
| CF-07 | R5, p.17 | React: empty | Filtrar por um texto único que não exista no banco de teste. Conferir resposta vazia e mensagem correspondente, sem excluir dados para criar o cenário. | [Empty](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/03-empty.png) |
| CF-08 | R5, p.17 | React: success | Remover o filtro anterior e carregar tarefas reais de teste. Conferir dados, filtros, ordenação, paginação e URL. | [Success](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/04-success.png) |
| CF-09 | R5, p.17 | React: error | Com a página já aberta, interromper a rede pelo navegador e executar nova consulta. Conferir mensagem de erro e recuperação após restabelecer a conexão; a interface não deve ficar presa em loading. | [Erro de rede](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/05-error-network.png) |
| CF-10 | R5, p.17 | React: Problem Details | Em cadastro autenticado, usar entrada que passe pelas restrições HTML e viole uma regra do servidor, como prioridade alta sem prazo. Conferir o 400 real e os erros por campo devolvidos pela API. Registrar o cenário e sua resposta; não fabricar o corpo do erro. | [Problem Details anterior](evidencias/roteiro-05-ui/2026-10-28T18-54-28-931Z/06-problem-details.png) |

Cada item CF-01 a CF-10 permanece **pendente para a nova etapa visual**, por decisão expressa do usuário. Isso não apaga evidências anteriores válidas nem transforma uma imagem histórica em captura da versão final. Se uma alteração técnica invalidar determinada evidência, a exigência correspondente deve ficar pendente até sua nova execução/captura.

## Registros do roteiro 8

- [ ] Quando Docker estiver disponível, registrar build, execução, usuário do contêiner, portas, HTTPS, liveness/readiness e versão por comandos/logs reais. Uma eventual captura complementar só deverá ocorrer na etapa visual autorizada.
- [ ] Quando houver GitHub remoto autorizado, registrar a URL e o resultado do workflow aprovado e do ensaio isolado de falha que bloqueia a publicação. Não apresentar validação estática como execução remota.
- [ ] Quando houver destino CD autorizado, registrar artefato/commit, configuração externa, migrations controladas, smoke, Health Check, promoção e rollback. Não criar um destino fictício para preencher uma imagem.

Os cenários dependentes de infraestrutura permanecem bloqueados enquanto ela não estiver disponível. Não há ZIP definitivo nesta etapa; as capturas futuras serão acrescentadas antes do fechamento da entrega acadêmica.
