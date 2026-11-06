# Validação documental reproduzível

Pré-requisitos: Python 3.10 ou posterior e `PyPDF2==3.0.1`. A ferramenta somente lê as fontes e os PDFs e grava seu resultado JSON; não regenera relatórios, executa testes da aplicação nem modifica evidências históricas.

Na pasta `PortalTarefas`:

```powershell
python -m pip install PyPDF2==3.0.1
python scripts/Validate-Documents.py
```

O resultado padrão é `docs/evidencias/finalizacao-20261106/validacao-documental.json`. Para uma nova auditoria histórica, informe outro caminho com `--output docs/evidencias/nova-auditoria/validacao-documental.json`.

O script verifica os 248 identificadores, sua distribuição por roteiro, classificações, links locais, abertura dos oito PDFs, títulos dos relatórios, SHA256/manifesto e correspondência do anexo de código. Código de saída zero significa que essas verificações passaram; não significa que todos os requisitos acadêmicos estejam concluídos. Requisitos pendentes, bloqueados e limitações históricas continuam explícitos na saída.

Para comparar também os textos R1–R8 com as páginas indicadas da fonte acadêmica, informe o PDF fornecido pela disciplina:

```powershell
python scripts/Validate-Documents.py --academic-pdf 'D:\FACUL_PROJETOS\DESENVOLVIMENTO_WEB_COM.NET\Roteiros.pdf'
```

O caminho acima corresponde ao ambiente da auditoria. Em outra instalação, use o local real do documento. O PDF acadêmico permanece externo ao pacote; a ferramenta registra somente caminho, hash e comparação. Os quatro itens gerais são sínteses da página 2 e requerem revisão humana. Links externos, âncoras, legibilidade visual, resultados de execução e a veracidade das classificações precisam das verificações correspondentes.
