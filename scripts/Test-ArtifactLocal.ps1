[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArtifactPath,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$ExpectedCommit,
    [ValidateRange(1024, 65535)][int]$HttpsPort = 8240,
    [ValidateRange(1024, 65535)][int]$HttpPort = 8241,
    [string]$ResultsDirectory = 'artifacts/local-homologation',
    [ValidateSet('Development', 'Production')][string]$Environment = 'Production',
    [switch]$InitializeDatabase,
    [switch]$IgnorarCertificadoLocal
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Homologação de artefato em loopback: não instala serviços nem publica externamente.
# Segredos são herdados pelo processo filho, nunca inseridos na linha de comando.
if ([string]::IsNullOrWhiteSpace($env:PORTAL_TEST_CONNECTION)) {
    throw 'Defina PORTAL_TEST_CONNECTION para um banco exclusivo de testes.'
}
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
try { $connection.set_ConnectionString($env:PORTAL_TEST_CONNECTION) }
catch { throw 'Conexão de testes inválida; seu valor foi omitido.' }
$databaseKeys = @(@('Database', 'Initial Catalog') | Where-Object { $connection.ContainsKey($_) })
if ($databaseKeys.Count -ne 1) { throw 'Informe Database ou Initial Catalog uma única vez.' }
$database = [string]$connection[$databaseKeys[0]]
if ($database -cnotmatch '^PortalTarefas_Tests_[A-Za-z0-9_]+$' -or $connection.ContainsKey('AttachDBFilename')) {
    throw 'Homologação recusada: use um banco exclusivo PortalTarefas_Tests_<sufixo>, sem anexar arquivos.'
}
if ($HttpsPort -eq $HttpPort) { throw 'As portas HTTP e HTTPS devem ser diferentes.' }
if ([string]::IsNullOrWhiteSpace($env:PORTAL_LOCAL_CERTIFICATE_PATH) -or
    [string]::IsNullOrEmpty($env:PORTAL_LOCAL_CERTIFICATE_PASSWORD)) {
    throw 'Defina PORTAL_LOCAL_CERTIFICATE_PATH e PORTAL_LOCAL_CERTIFICATE_PASSWORD externamente.'
}
$certificate = (Resolve-Path -LiteralPath $env:PORTAL_LOCAL_CERTIFICATE_PATH).Path
if ([IO.Path]::GetExtension($certificate) -ne '.pfx') { throw 'O certificado local deve ser um PFX.' }
$artifact = (Resolve-Path -LiteralPath $ArtifactPath).Path
$assembly = Join-Path $artifact 'PortalTarefas.Web.dll'
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $artifact 'wwwroot/app/index.html') -PathType Leaf)) {
    throw 'Informe uma publicação existente que contenha API e build React integrado.'
}
$root = Split-Path -Parent $PSScriptRoot
$resultsRoot = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $root $ResultsDirectory }
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runDirectory = Join-Path $resultsRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$privateLogs = Join-Path $root ('.local/homologacao-logs/' + $runId)
New-Item -ItemType Directory -Path $privateLogs -Force | Out-Null

$settings = @{
    ConnectionStrings__PortalTarefas = $env:PORTAL_TEST_CONNECTION
    ASPNETCORE_ENVIRONMENT = $Environment
    DOTNET_ENVIRONMENT = $Environment
    Portal__Storage = 'SqlServer'
    Portal__FrontendOrigin = "https://localhost:$HttpsPort"
    AllowedHosts = 'localhost;127.0.0.1'
    Telemetry__Console = 'false'
    Seed__Enabled = 'false'
    Seed__SampleData = 'false'
    Seed__AdminEmail = ''
    Seed__AdminPassword = ''
    Seed__UserEmail = ''
    Seed__UserPassword = ''
    ASPNETCORE_URLS = "https://localhost:$HttpsPort;http://localhost:$HttpPort"
    ASPNETCORE_HTTPS_PORT = [string]$HttpsPort
    ASPNETCORE_Kestrel__Certificates__Default__Path = $certificate
    ASPNETCORE_Kestrel__Certificates__Default__Password = $env:PORTAL_LOCAL_CERTIFICATE_PASSWORD
}
$previous = @{}
$process = $null
$result = [ordered]@{
    instanteUtc = [DateTimeOffset]::UtcNow.ToString('O')
    ambiente = $Environment
    bancoExclusivo = $database
    commitEsperado = $ExpectedCommit
    artefato = $artifact
    assemblySha256 = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
    migrationsSolicitadas = [bool]$InitializeDatabase
    resultado = 'Reprovado'
    processoEncerrado = $false
    semCapturasDeTela = $true
}
try {
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    if ($InitializeDatabase) {
        # Inicialização não importa contas/segredos do perfil Development do usuário.
        $env:ASPNETCORE_ENVIRONMENT = 'Testing'
        $env:DOTNET_ENVIRONMENT = 'Testing'
        & dotnet $assembly --contentRoot $artifact --initialize *> (Join-Path $privateLogs 'initialize.txt')
        if ($LASTEXITCODE -ne 0) { throw 'A inicialização do banco exclusivo falhou; consulte o log local privado.' }
        $env:ASPNETCORE_ENVIRONMENT = $Environment
        $env:DOTNET_ENVIRONMENT = $Environment
    }
    $start = @{
        FilePath = (Get-Command dotnet -ErrorAction Stop).Source
        ArgumentList = @(('"' + $assembly + '"'), '--contentRoot', ('"' + $artifact + '"'))
        WorkingDirectory = $artifact
        RedirectStandardOutput = (Join-Path $privateLogs 'server-output.txt')
        RedirectStandardError = (Join-Path $privateLogs 'server-error.txt')
        PassThru = $true
    }
    if ($env:OS -eq 'Windows_NT') { $start['WindowStyle'] = 'Hidden' }
    $process = Start-Process @start
    $smoke = & (Join-Path $PSScriptRoot 'Smoke-Test.ps1') -BaseUrl "https://localhost:$HttpsPort" `
        -VersaoEsperada $ExpectedCommit -Tentativas 20 -IgnorarCertificadoLocal:$IgnorarCertificadoLocal
    if ($process.HasExited) { throw 'O processo da aplicação terminou durante a homologação.' }
    $result['smoke'] = $smoke
    $result['resultado'] = 'Aprovado'
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
        $result['processoEncerrado'] = $true
        $process.Dispose()
    }
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runDirectory 'homologacao.json') -Encoding UTF8
}
Write-Output "Homologação $Environment aprovada; artefato preservado e processo de teste encerrado. Evidência: $runDirectory"
