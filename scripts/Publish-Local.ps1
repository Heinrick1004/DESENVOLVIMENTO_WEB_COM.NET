[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^portaltarefas:[a-z0-9][a-z0-9._-]{0,127}$')]
    [string]$Imagem,
    [Parameter(Mandatory)]
    [string]$ArquivoAmbiente,
    [Parameter(Mandatory)]
    [string]$CertificadoPfx,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,62}$')]
    [string]$Nome = 'portal-homologacao',
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,62}$')]
    [string]$Rede = 'portal-tarefas_default',
    [ValidateRange(1024, 65535)]
    [int]$PortaHttps = 8443,
    [switch]$IgnorarCertificadoLocal
)

$ErrorActionPreference = 'Stop'
Get-Command docker -ErrorAction Stop | Out-Null
$ambiente = (Resolve-Path -LiteralPath $ArquivoAmbiente -ErrorAction Stop).Path
$certificado = (Resolve-Path -LiteralPath $CertificadoPfx -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $ambiente -PathType Leaf)) { throw 'O arquivo de ambiente deve ser um arquivo regular.' }
if (-not (Test-Path -LiteralPath $certificado -PathType Leaf) -or [IO.Path]::GetExtension($certificado) -ne '.pfx') {
    throw 'Informe um certificado PFX existente.'
}
if ($certificado.Contains(',')) { throw 'Use um caminho de certificado sem vírgulas para o volume Docker.' }

# Verifica a presença das chaves sem imprimir os valores do arquivo externo.
$linhas = Get-Content -LiteralPath $ambiente
foreach ($chave in @('ConnectionStrings__PortalTarefas', 'ASPNETCORE_Kestrel__Certificates__Default__Password')) {
    if (-not ($linhas -match ('^' + [Regex]::Escape($chave) + '=.+$'))) {
        throw "O arquivo externo precisa definir $chave."
    }
}

$existentes = & docker ps --all --format '{{.Names}}'
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar o Docker.' }
if ($existentes -contains $Nome) { throw 'Já existe um contêiner com esse nome. Escolha outro nome para preservar a versão anterior.' }
$versao = & docker image inspect $Imagem --format '{{ index .Config.Labels "org.opencontainers.image.revision" }}'
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($versao)) { throw 'A imagem local não existe ou não contém a identificação de versão.' }
& docker network inspect $Rede --format '{{.Name}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'A rede local não existe. Inicie primeiro o banco pelo Compose.' }

$argumentos = @(
    'run', '--detach', '--name', $Nome, '--restart', 'unless-stopped',
    '--network', $Rede, '--publish', "127.0.0.1:${PortaHttps}:8443",
    '--env-file', $ambiente,
    '--env', 'ASPNETCORE_ENVIRONMENT=Production',
    '--env', 'Portal__Storage=SqlServer',
    '--env', 'AllowedHosts=localhost;127.0.0.1',
    '--env', "Portal__FrontendOrigin=https://localhost:$PortaHttps",
    '--env', 'ASPNETCORE_HTTP_PORTS=8080', '--env', 'ASPNETCORE_HTTPS_PORTS=8443',
    '--env', 'ASPNETCORE_Kestrel__Certificates__Default__Path=/https/portal.pfx',
    '--mount', "type=bind,source=$certificado,target=/https/portal.pfx,readonly",
    '--mount', "type=volume,source=$Nome-chaves,target=/home/app/.aspnet/DataProtection-Keys",
    $Imagem
)
& docker @argumentos | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar a homologação local. A versão anterior foi preservada.' }

try {
    & (Join-Path $PSScriptRoot 'Smoke-Test.ps1') -BaseUrl "https://localhost:$PortaHttps" `
        -VersaoEsperada $versao.Trim() -Tentativas 30 -IgnorarCertificadoLocal:$IgnorarCertificadoLocal
}
catch {
    Write-Warning 'O contêiner foi preservado para diagnóstico. Nenhum volume ou versão anterior foi removido.'
    throw
}
