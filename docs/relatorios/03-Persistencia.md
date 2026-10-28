# Atividade 3 — persistência, consultas e concorrência

O domínio cumulativo utiliza `Tarefa`, `Usuario : IdentityUser` e `Auditoria`. `PortalTarefasDbContext` deriva de `IdentityDbContext<Usuario>` e recebe somente as opções no construtor, permitindo pooling sem guardar dados do usuário no contexto. O SQL Server mantém os usuários, tarefas e eventos de auditoria em tabelas próprias.

## Decisões implementadas

- O banco limita os tamanhos de título, descrição, situação, equipe e identificadores, verifica a prioridade entre 1 e 3 e restringe a situação a Pendente, EmAndamento ou Concluida. Há índices para equipe/situação/prazo, publicação/prazo, proprietário e histórico.
- `rowversion` fornece concorrência otimista. O cliente recebe a versão em Base64 de oito bytes e precisa enviá-la ao alterar ou excluir. Uma versão obsoleta resulta em `ConflitoException`; o serviço não sobrescreve a edição mais recente.
- A criação e sua auditoria utilizam uma transação explícita porque o identificador da tarefa é gerado pelo banco. Alteração e exclusão salvam o recurso e o evento em um único `SaveChangesAsync`, que mantém a atomicidade relacional. `Auditoria.TarefaId` é um identificador histórico sem FK, preservando o registro após exclusão.
- As transações explícitas são executadas pela estratégia do provider, mantendo compatibilidade com repetição de falhas transitórias. Ao repetir uma criação após resultado incerto de commit, o serviço procura o identificador, o instante original e o respectivo evento antes de inserir novamente. A carga de exemplos testa a existência de tarefas dentro de transação serializável.
- A consulta usa `AsNoTracking`, filtros antes da paginação, tamanho máximo 50, `CountAsync`, `Skip`/`Take` e projeção para DTO. Os nomes de ordenação pertencem a uma lista permitida, sem interpolação de SQL. Um identificador desempata a ordenação.
- A visibilidade considera publicação, autenticação e equipe; o administrador autenticado visualiza todas as tarefas. Criação exige login e equipe própria, com exceção administrativa. Edição permite proprietário, integrante da equipe ou claim específica para outra equipe. Exclusão exige o perfil Administrador.
- O painel EF e o painel ADO.NET aplicam os mesmos critérios de visibilidade e de vencimento. A consulta ADO.NET parametriza situação, data, publicação, autenticação, perfil e equipe; a conexão é fechada pelo método somente quando ele próprio a abriu. A data civil usa o fuso de São Paulo.
- A validação cruzada exige prazo e descrição com pelo menos dez caracteres úteis para prioridade alta, tanto no DTO quanto nas chamadas diretas ao serviço.

## Modo Memory e carga inicial

`InMemoryTarefaService` é uma alternativa explicitamente selecionada, registrada como singleton. Um lock cobre leitura, alteração, comparação de versões e auditoria, garantindo que somente uma atualização da mesma versão seja aceita. Seus dados são voláteis e o painel ADO.NET informa indisponibilidade, pois esse modo não possui conexão relacional.

`SeedData` cria os perfis Usuario/Administrador de forma idempotente. Contas iniciais dependem de `Seed:AdminEmail`, `Seed:AdminPassword`, `Seed:UserEmail` e `Seed:UserPassword` em configuração externa; não há senha fixa ou registro de credenciais em log. A conta administrativa recebe a claim `tarefas:editar-outra-equipe=true`. Os exemplos são opcionais, ativados por `Seed:SampleData`, e só são incluídos quando não há tarefas.

`DesignTimeDbContextFactory` permite gerar migrações sem iniciar a aplicação. A conexão real pode ser fornecida por `ConnectionStrings__PortalTarefas`; a fábrica não aplica migrações automaticamente.

## Verificação e limites

Foram preparados testes em `DomainTests.cs` e `InMemoryTests.cs` para validação cruzada, formato da versão, publicação/equipes, paginação, filtros, lista permitida de ordenação, autorização, colisão entre duas edições, exclusão com versão e preservação da auditoria. Este relatório, na sua redação inicial, não registra execução ou aprovação desses testes; os resultados devem ser acrescentados após a execução real pelo responsável pela integração.

A aprovação dos testes do modo Memory não comprova conectividade SQL Server, aplicação de migrações, tipo `rowversion` no servidor, rollback relacional ou equivalência real entre os painéis EF/ADO.NET. Essas verificações exigem uma instância SQL Server configurada e disponível. Não se deve registrar esses itens como verificados com base apenas nos testes unitários.
