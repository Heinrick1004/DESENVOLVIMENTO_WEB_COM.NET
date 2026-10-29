[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$raizProjeto = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$raizGit = $raizProjeto.Replace('\', '/')
$destinoInventario = Join-Path $raizProjeto 'docs/INVENTARIO_ENTREGA.json'

# Este procedimento apenas lê as fontes e escreve o inventário. Não cria ZIP,
# não copia a árvore, não remove caches e não altera imagens ou relatórios.
$referenciaGit = & git -c "safe.directory=$raizGit" -C $raizProjeto rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar o commit observado.' }
$branchGit = & git -c "safe.directory=$raizGit" -C $raizProjeto branch --show-current
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar a branch.' }
$arquivosGit = & git -c "safe.directory=$raizGit" -c core.quotePath=false -C $raizProjeto ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível listar os arquivos versionados e novos não ignorados.' }

$essenciais = @()
$excluidosDaSelecao = @()
foreach ($caminhoRelativo in ($arquivosGit | Sort-Object -Unique)) {
    if ([string]::IsNullOrWhiteSpace($caminhoRelativo)) { continue }
    $relativo = $caminhoRelativo.Replace('\', '/')
    if ($relativo -eq 'docs/INVENTARIO_ENTREGA.json') { continue }
    $motivo = $null
    if ($relativo -match '(^|/)(bin|obj|node_modules|dist|artifacts|TestResults)(/|$)' -or
        $relativo -match '^(\.git|\.local|\.tools|\.nuget)(/|$)' -or
        $relativo -match '^src/PortalTarefas\.Web/wwwroot/app(/|$)') {
        $motivo = 'Saída restaurável, cache, ferramenta baixada ou ambiente local.'
    }
    elseif ($relativo -match '\.(pfx|p12|key|pem|mdf|ldf|bak|tmp|tsbuildinfo)$' -or
        $relativo -match '(^|/)secrets\.json$' -or
        ($relativo -match '(^|/)\.env($|\.)' -and $relativo -notmatch '\.env\.example$')) {
        $motivo = 'Arquivo privado/local ou temporário; não incluir na entrega.'
    }
    if ($motivo) {
        $excluidosDaSelecao += [ordered]@{ caminho = $relativo; motivo = $motivo }
        continue
    }

    $caminhoCompleto = Join-Path $raizProjeto $relativo
    if (-not (Test-Path -LiteralPath $caminhoCompleto -PathType Leaf)) {
        throw "Arquivo selecionado não existe: $relativo"
    }
    $grupo = switch -Regex ($relativo) {
        '^docs/relatorios/' { 'Relatórios e anexos'; break }
        '^docs/evidencias/' { 'Evidências reais'; break }
        '^docs/' { 'Documentação e contrato'; break }
        '^src/PortalTarefas.Web/Data/Migrations/' { 'Migrations'; break }
        '^src/PortalTarefas.Web/' { 'Backend'; break }
        '^src/PortalTarefas.Frontend/' { 'Frontend e contrato gerado'; break }
        '^tests/' { 'Testes'; break }
        '^scripts/' { 'Scripts reproduzíveis'; break }
        '^\.github/' { 'CI/CD'; break }
        default { 'Solução e configuração' }
    }
    $arquivo = Get-Item -LiteralPath $caminhoCompleto
    $hash = Get-FileHash -LiteralPath $caminhoCompleto -Algorithm SHA256
    $essenciais += [ordered]@{
        caminho = $relativo
        grupo = $grupo
        bytes = $arquivo.Length
        sha256 = $hash.Hash.ToLowerInvariant()
    }
}

$gruposRestauraveis = @(
    @{ caminho = 'src/PortalTarefas.Web/bin'; restaurarCom = 'dotnet build/publish' },
    @{ caminho = 'src/PortalTarefas.Web/obj'; restaurarCom = 'dotnet restore/build' },
    @{ caminho = 'tests/PortalTarefas.Tests/bin'; restaurarCom = 'dotnet build/test' },
    @{ caminho = 'tests/PortalTarefas.Tests/obj'; restaurarCom = 'dotnet restore/build' },
    @{ caminho = 'src/PortalTarefas.Frontend/node_modules'; restaurarCom = 'npm ci' },
    @{ caminho = 'src/PortalTarefas.Frontend/dist'; restaurarCom = 'npm run build' },
    @{ caminho = 'src/PortalTarefas.Web/wwwroot/app'; restaurarCom = 'Build React e cópia conforme README/Dockerfile' },
    @{ caminho = '.tools'; restaurarCom = 'Instalar/restaurar ferramentas documentadas, sem incluir os downloads' },
    @{ caminho = '.nuget'; restaurarCom = 'dotnet restore/tool restore' },
    @{ caminho = '.local'; restaurarCom = 'Recriar ambiente privado conforme README; nunca copiar segredos' },
    @{ caminho = 'artifacts'; restaurarCom = 'Build/publish; preservar evidências acadêmicas em docs/evidencias' },
    @{ caminho = 'TestResults'; restaurarCom = 'dotnet test; preservar resultados pertinentes em docs/evidencias' }
) | ForEach-Object {
    [ordered]@{
        caminho = $_.caminho
        presenteNaPastaDeTrabalho = (Test-Path -LiteralPath (Join-Path $raizProjeto $_.caminho))
        restaurarCom = $_.restaurarCom
        acaoNestaExecucao = 'Somente identificação; nada removido.'
    }
}

$inventario = [ordered]@{
    instanteUtc = [DateTimeOffset]::UtcNow.ToString('o')
    natureza = 'Inventário preparatório de entrega; nenhum ZIP ou cópia de arquivos foi criado.'
    branchObservada = ($branchGit -join '').Trim()
    commitObservadoNaGeracao = ($referenciaGit -join '').Trim()
    observacaoGit = 'Os hashes incluem arquivos atuais, inclusive novos ainda não commitados. O commit final da entrega é registrado depois no Git.'
    quantidadeEssenciais = $essenciais.Count
    bytesEssenciais = ($essenciais | Measure-Object -Property bytes -Sum).Sum
    essenciais = $essenciais
    indiceAutorreferente = 'docs/INVENTARIO_ENTREGA.json é essencial, mas não recebe hash de si próprio neste índice.'
    gruposRestauraveisOuPrivados = @($gruposRestauraveis)
    arquivosExcluidosDaSelecaoGit = @($excluidosDaSelecao)
    limites = @(
        'Nenhuma imagem ou relatório é modificado pelo script.',
        'Não existe inspeção de valores secretos neste inventário; a revisão de segredos é uma verificação separada.',
        'Arquivos ignorados pelo Git não são enumerados como fontes essenciais.',
        'Os contratos OpenAPI/schema.d.ts e as evidências reais são preservados como arquivos essenciais.',
        'O ZIP definitivo aguarda as capturas finais autorizadas.'
    )
}
[IO.File]::WriteAllText($destinoInventario, ($inventario | ConvertTo-Json -Depth 8) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{
    Inventario = $destinoInventario
    ArquivosEssenciais = $essenciais.Count
    ExcluidosDaSelecaoGit = $excluidosDaSelecao.Count
    ZipCriado = $false
    ArquivosRemovidos = 0
}
