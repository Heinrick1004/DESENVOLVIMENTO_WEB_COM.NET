[CmdletBinding()]
param(
    [switch]$CreateZip,
    [string]$OutputDirectory = 'artifacts/entrega'
)

$ErrorActionPreference = 'Stop'
$raizProjeto = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$raizGit = $raizProjeto.Replace('\', '/')
$destinoInventario = Join-Path $raizProjeto 'docs/INVENTARIO_ENTREGA.json'

# A seleção é explícita, baseada no Git e nas fontes atuais. Nenhum cache,
# banco, volume ou arquivo de origem é removido. ZIP somente com -CreateZip.
$referenciaGit = & git -c "safe.directory=$raizGit" -C $raizProjeto rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar o commit observado.' }
$branchGit = & git -c "safe.directory=$raizGit" -C $raizProjeto branch --show-current
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar a branch.' }
$estadoGit = @(& git -c "safe.directory=$raizGit" -C $raizProjeto status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível inspecionar as alterações locais.' }
$fontesModificadas = $estadoGit.Count -gt 0
$arquivosGit = & git -c "safe.directory=$raizGit" -c core.quotePath=false -C $raizProjeto ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível listar os arquivos versionados e novos não ignorados.' }

$essenciais = @()
$excluidosDaSelecao = @()
foreach ($caminhoRelativo in ($arquivosGit | Sort-Object -Unique)) {
    if ([string]::IsNullOrWhiteSpace($caminhoRelativo)) { continue }
    $relativo = $caminhoRelativo.Replace('\', '/')
    if ($relativo -eq 'docs/INVENTARIO_ENTREGA.json') { continue }
    $motivo = $null
    if ($relativo -match '(^|/)(bin|obj|node_modules|dist|artifacts|TestResults|__pycache__)(/|$)' -or
        $relativo -match '^(\.git|\.local|\.tools|\.nuget)(/|$)' -or
        $relativo -match '^src/PortalTarefas\.Web/wwwroot/app(/|$)') {
        $motivo = 'Saída restaurável, cache, ferramenta baixada ou ambiente local.'
    }
    elseif ($relativo -match '\.(pfx|p12|key|pem|mdf|ldf|bak|tmp|tsbuildinfo|pyc|pyo)$' -or
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

$bytesEssenciais = [long]0
foreach ($item in $essenciais) { $bytesEssenciais += [long]$item['bytes'] }
$inventario = [ordered]@{
    instanteUtc = [DateTimeOffset]::UtcNow.ToString('o')
    natureza = 'Inventário das fontes atuais selecionadas para entrega; ZIP opcional com -CreateZip.'
    branchObservada = ($branchGit -join '').Trim()
    commitObservadoNaGeracao = ($referenciaGit -join '').Trim()
    fontesModificadas = $fontesModificadas
    observacaoGit = 'O SHA identifica a origem; fontesModificadas informa se há alterações locais. O inventário é metadado gerado, ignorado pelo Git e incluído no ZIP.'
    quantidadeEssenciais = $essenciais.Count
    bytesEssenciais = $bytesEssenciais
    essenciais = $essenciais
    indiceAutorreferente = 'docs/INVENTARIO_ENTREGA.json é essencial, mas não recebe hash de si próprio neste índice.'
    gruposRestauraveisOuPrivados = @($gruposRestauraveis)
    arquivosExcluidosDaSelecaoGit = @($excluidosDaSelecao)
    limites = @(
        'Nenhuma imagem ou relatório é modificado pelo script.',
        'Não existe inspeção de valores secretos neste inventário; a revisão de segredos é uma verificação separada.',
        'Arquivos ignorados pelo Git não são enumerados como fontes essenciais.',
        'Os contratos OpenAPI/schema.d.ts e as evidências reais são preservados como arquivos essenciais.',
        'A existência do ZIP não significa aprovação acadêmica: consultar STATUS_DE_EXECUCAO.md e os relatórios de verificação.'
    )
}
[IO.File]::WriteAllText($destinoInventario, ($inventario | ConvertTo-Json -Depth 8) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
if ($CreateZip) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $outputRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $raizProjeto $OutputDirectory)) }
    if (-not $outputRoot.StartsWith($raizProjeto + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O destino da entrega deve permanecer dentro da pasta do projeto.'
    }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $zipPath = Join-Path $outputRoot 'UNIP_Desenvolvimento_Web_NET_PortalTarefas_Entrega_Final.zip'
    if (Test-Path -LiteralPath $zipPath) { throw 'O ZIP de destino já existe. Use outro OutputDirectory para preservar a entrega anterior.' }
    $archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $essenciais) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $raizProjeto $item.caminho), ('PortalTarefas/' + $item.caminho), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $destinoInventario, 'PortalTarefas/docs/INVENTARIO_ENTREGA.json', [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    } finally { $archive.Dispose() }
    $summary = [ordered]@{
        instanteUtc = [DateTimeOffset]::UtcNow.ToString('o')
        zip = $zipPath
        bytes = (Get-Item -LiteralPath $zipPath).Length
        sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        arquivos = $essenciais.Count + 1
        commitOrigem = ($referenciaGit -join '').Trim()
        fontesModificadas = $fontesModificadas
        versao = if ($fontesModificadas) { 'Snapshot local identificado por inventário SHA256; há alterações ainda não commitadas.' } else { 'Fontes do commit Git identificado, sem alterações locais; inventário gerado após o commit.' }
        verificacao = 'Executar Verify-Delivery.py e validar restore/build na cópia extraída antes de considerar o pacote validado.'
    }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'pacote.json'), ($summary | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Write-Output ([pscustomobject]$summary)
    return
}
[pscustomobject]@{
    Inventario = $destinoInventario
    ArquivosEssenciais = $essenciais.Count
    ExcluidosDaSelecaoGit = $excluidosDaSelecao.Count
    ZipCriado = $false
    ArquivosRemovidos = 0
}
