[CmdletBinding()]
param(
    [string]$ApiUrl = 'http://127.0.0.1:5240',
    [string]$DocumentUrl
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $projectRoot 'src/PortalTarefas.Frontend'
$documentPath = Join-Path $projectRoot 'docs/openapi.json'

if ([string]::IsNullOrWhiteSpace($DocumentUrl)) {
    $DocumentUrl = $ApiUrl.TrimEnd('/') + '/openapi/v1.json'
}
$documentUri = [Uri]$DocumentUrl
if (-not $documentUri.IsAbsoluteUri -or $documentUri.Scheme -notin @('http', 'https') -or $documentUri.UserInfo) {
    throw 'Informe uma URL HTTP(S) absoluta, sem credenciais, para a API ou documento OpenAPI.'
}

# O documento vem da aplicação ASP.NET Core em execução. Não manter um contrato
# escrito à mão separado das rotas, DTOs e metadados publicados pelo servidor.
$response = Invoke-WebRequest -Uri $documentUri -UseBasicParsing -TimeoutSec 30
$document = $response.Content | ConvertFrom-Json
if (-not $document.openapi -or -not $document.paths -or -not $document.paths.'/api/tarefas') {
    throw 'A resposta não contém o documento OpenAPI esperado do Portal de Tarefas.'
}
[IO.File]::WriteAllText($documentPath, $response.Content, [Text.UTF8Encoding]::new($false))
Write-Output "OpenAPI exportado de: $documentUri"
Write-Output "Documento: $documentPath"

$npmCommand = if ($env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }
Push-Location $frontend
try {
    & $npmCommand run generate:client
    if ($LASTEXITCODE -ne 0) { throw "A geração TypeScript falhou (código $LASTEXITCODE)." }
}
finally { Pop-Location }
Write-Output 'Cliente tipado gerado em src/PortalTarefas.Frontend/src/api/schema.d.ts.'
