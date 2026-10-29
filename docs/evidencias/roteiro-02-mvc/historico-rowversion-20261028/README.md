# Registro anterior à correção de RowVersion

A captura de 28/10/2026 preserva um defeito encontrado durante a verificação real: o formulário de criação apresentava a mensagem `The Versão da tarefa field is required.`. A versão de concorrência era implicitamente obrigatória por ser uma propriedade de referência não anulável, embora a criação ainda não possua uma versão do banco.

Este material registra o achado e não representa a evidência final da interface corrigida. A captura e o manifesto atuais ficam na pasta acima. A edição continua exigindo a versão por validação explícita no servidor.

Não houve interceptação HTTP, alteração do DOM ou criação de tarefa válida nessa verificação. O manifesto preservado registra os valores mantidos no formulário e a contagem de tarefas antes e depois.
