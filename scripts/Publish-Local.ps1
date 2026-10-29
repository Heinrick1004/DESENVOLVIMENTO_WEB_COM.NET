[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^portaltarefas:[a-f0-9]{40}$')]
    [string]$Imagem,
    [Parameter(Mandatory)]
    [string]$ArquivoAmbiente,
    [Parameter(Mandatory)]
    [string]$CertificadoPfx,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,62}$')]
    [string]$Nome = 'portal-homologacao',
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,62}$')]
    [string]$Rede = 'portal-tarefas-tests_default',
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,62}$')]
    [string]$VolumeChaves = 'portal-tarefas-tests-chaves',
    [ValidateRange(1024, 65535)]
    [int]$PortaHttps = 8443,
    [switch]$InitializeDatabase,
    [ValidatePattern('^$|^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,62}$')]
    [string]$NomeAnterior = '',
    [ValidateRange(1024, 65535)]
    [int]$PortaAnterior = 8443,
    [string]$ResultsDirectory = 'artifacts/deployments',
    [switch]$IgnorarCertificadoLocal
)

$ErrorActionPreference = 'Stop'
$ambiente = (Resolve-Path -LiteralPath $ArquivoAmbiente -ErrorAction Stop).Path
$certificado = (Resolve-Path -LiteralPath $CertificadoPfx -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $ambiente -PathType Leaf)) { throw 'O arquivo de ambiente deve ser um arquivo regular.' }
if (-not (Test-Path -LiteralPath $certificado -PathType Leaf) -or [IO.Path]::GetExtension($certificado) -ne '.pfx') {
    throw 'Informe um certificado PFX existente.'
}
if ($certificado.Contains(',')) { throw 'Use um caminho de certificado sem vírgulas para o volume Docker.' }

# O arquivo Docker usa KEY=VALUE; valores e credenciais nunca sao impressos.
$configuracao = @{}
foreach ($linha in (Get-Content -LiteralPath $ambiente)) {
    if ($linha -match '^\s*(#|$)') { continue }
    $par = [Regex]::Match($linha, '^(?<chave>[A-Za-z_][A-Za-z0-9_]*)=(?<valor>.*)$')
    if (-not $par.Success) { throw 'Use KEY=VALUE por linha e __ para opcoes hierarquicas, sem aliases com dois-pontos.' }
    $chave = $par.Groups['chave'].Value
    if ($configuracao.ContainsKey($chave)) { throw "O arquivo externo repete a chave $chave." }
    $configuracao[$chave] = $par.Groups['valor'].Value
}
foreach ($chave in @('ConnectionStrings__PortalTarefas', 'ASPNETCORE_Kestrel__Certificates__Default__Password')) {
    if ([string]::IsNullOrWhiteSpace($configuracao[$chave])) {
        throw "O arquivo externo precisa definir $chave."
    }
}
$conexao = New-Object System.Data.Common.DbConnectionStringBuilder
try { $conexao.set_ConnectionString($configuracao['ConnectionStrings__PortalTarefas']) }
catch { throw 'A conexao externa e invalida. O valor foi omitido.' }
$chavesBanco = @(@('Initial Catalog', 'Database') | Where-Object { $conexao.ContainsKey($_) })
if ($chavesBanco.Count -ne 1) { throw 'Informe uma unica chave Database ou Initial Catalog para o banco de testes.' }
$banco = [string]$conexao[$chavesBanco[0]]
if ($banco -cnotmatch '^PortalTarefas_Tests_[A-Za-z0-9_]+$' -or $conexao.ContainsKey('AttachDBFilename')) {
    throw 'A homologacao aceita apenas bancos exclusivos PortalTarefas_Tests_<sufixo>, sem arquivos anexados.'
}

Get-Command docker -ErrorAction Stop | Out-Null

$existentes = & docker ps --all --format '{{.Names}}'
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar o Docker.' }
if ($existentes -contains $Nome) { throw 'Já existe um contêiner com esse nome. Escolha outro nome para preservar a versão anterior.' }
$labelsJson = & docker image inspect $Imagem --format '{{json .Config.Labels}}'
if ($LASTEXITCODE -ne 0) { throw 'A imagem local nao existe.' }
$labels = $labelsJson | ConvertFrom-Json
$versao = [string]$labels.'org.opencontainers.image.revision'
if ([string]::IsNullOrWhiteSpace($versao)) { throw 'A imagem local nao contem a identificacao de versao.' }
if ($versao.Trim() -cne $Imagem.Split(':')[1]) { throw 'A tag da imagem e o commit declarado no label devem ser iguais.' }
$imagemId = & docker image inspect $Imagem --format '{{.Id}}'
if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel identificar a imagem local.' }
& docker network inspect $Rede --format '{{.Name}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'A rede local não existe. Inicie primeiro o banco pelo Compose.' }
$versaoAnterior = $null
if ($NomeAnterior) {
    if ($NomeAnterior -eq $Nome) { throw 'A nova instancia precisa de um nome diferente da anterior.' }
    $labelsAnterioresJson = & docker container inspect $NomeAnterior --format '{{json .Config.Labels}}'
    if ($LASTEXITCODE -ne 0) { throw 'A instancia anterior nao foi encontrada.' }
    $labelsAnteriores = $labelsAnterioresJson | ConvertFrom-Json
    $versaoAnterior = [string]$labelsAnteriores.'org.opencontainers.image.revision'
    if ($versaoAnterior -cnotmatch '^[a-f0-9]{40}$') {
        throw 'A instancia anterior deve existir e informar seu commit para preparar o rollback.'
    }
}

if ($InitializeDatabase) {
    # A mesma imagem que sera publicada aplica migrations apenas mediante este
    # switch explicito e depois encerra; nenhuma instancia anterior e parada.
    & docker run --rm --network $Rede --env-file $ambiente `
        --env ASPNETCORE_ENVIRONMENT=Production --env DOTNET_ENVIRONMENT=Production --env Portal__Storage=SqlServer `
        --env Seed__Enabled=false $Imagem --initialize
    if ($LASTEXITCODE -ne 0) { throw 'A inicializacao controlada falhou. A nova instancia nao sera iniciada.' }
}

$argumentos = @(
    'run', '--detach', '--name', $Nome, '--restart', 'unless-stopped',
    '--network', $Rede, '--publish', "127.0.0.1:${PortaHttps}:8443",
    '--env-file', $ambiente,
    '--env', 'ASPNETCORE_ENVIRONMENT=Production',
    '--env', 'DOTNET_ENVIRONMENT=Production',
    '--env', 'Portal__Storage=SqlServer',
    '--env', 'Seed__Enabled=false',
    '--env', 'AllowedHosts=localhost;127.0.0.1',
    '--env', "Portal__FrontendOrigin=https://localhost:$PortaHttps",
    '--env', 'ASPNETCORE_HTTP_PORTS=8080', '--env', 'ASPNETCORE_HTTPS_PORTS=8443',
    '--env', 'ASPNETCORE_Kestrel__Certificates__Default__Path=/https/portal.pfx',
    '--mount', "type=bind,source=$certificado,target=/https/portal.pfx,readonly",
    '--mount', "type=volume,source=$VolumeChaves,target=/home/app/.aspnet/DataProtection-Keys",
    $Imagem
)
& docker @argumentos | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar a homologação local. A versão anterior foi preservada.' }

try {
    $smoke = & (Join-Path $PSScriptRoot 'Smoke-Test.ps1') -BaseUrl "https://localhost:$PortaHttps" `
        -VersaoEsperada $versao.Trim() -Tentativas 30 -IgnorarCertificadoLocal:$IgnorarCertificadoLocal
}
catch {
    Write-Warning 'O contêiner foi preservado para diagnóstico. Nenhum volume ou versão anterior foi removido.'
    throw
}

$raiz = Split-Path -Parent $PSScriptRoot
$resultados = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $raiz $ResultsDirectory }
New-Item -ItemType Directory -Path $resultados -Force | Out-Null
$identificador = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$manifesto = Join-Path $resultados ($identificador + '.json')
$excecaoCertificado = if ($IgnorarCertificadoLocal) { ' -IgnorarCertificadoLocal' } else { '' }
$rollback = @()
if ($NomeAnterior) {
    $rollback = @(
        "docker stop $Nome",
        "docker start $NomeAnterior",
        "./scripts/Smoke-Test.ps1 -BaseUrl https://localhost:$PortaAnterior -VersaoEsperada $($versaoAnterior.Trim())$excecaoCertificado"
    )
}
[ordered]@{
    instanteUtc = [DateTimeOffset]::UtcNow.ToString('O')
    imagem = $Imagem
    imagemId = $imagemId.Trim()
    commit = $versao.Trim()
    bancoExclusivo = $banco
    container = $Nome
    rede = $Rede
    volumeChaves = $VolumeChaves
    inicializacaoSolicitada = [bool]$InitializeDatabase
    smoke = $smoke
    promocao = 'Reutilizar esta imagem e seu commit, com aprovacao e configuracao externa do destino de teste.'
    rollback = [ordered]@{
        disponivel = [bool]$NomeAnterior
        containerAnterior = $NomeAnterior
        commitAnterior = $versaoAnterior
        comandos = $rollback
        dados = 'Reverter somente a aplicacao. Preservar volumes e avaliar compatibilidade das migrations; nao executar migrations Down automaticamente.'
    }
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifesto -Encoding UTF8
[pscustomobject]@{ Resultado = 'Aprovado'; Imagem = $Imagem; Commit = $versao.Trim(); Manifesto = $manifesto; Smoke = $smoke }
