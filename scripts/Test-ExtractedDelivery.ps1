[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$ResultsDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'PortalTarefas.sln') -PathType Leaf)) { throw 'Projeto extraído inválido.' }
$resultsRoot = [IO.Path]::GetFullPath($ResultsDirectory)
if (Test-Path -LiteralPath $resultsRoot) { throw 'Use um diretório de resultados novo para preservar evidências.' }
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
$steps = @()
function Invoke-DeliveryCheck([string]$name, [string]$tool, [string[]]$toolArguments) {
    # Uma ferramenta ausente não pode herdar o exit code zero da etapa anterior.
    $executable = Get-Command $tool -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $started = [DateTimeOffset]::UtcNow.ToString('o')
    $previousPolicy = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $global:LASTEXITCODE = -1
        & $executable.Source @toolArguments *> (Join-Path $resultsRoot ($name + '.txt'))
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPolicy }
    $script:steps += [ordered]@{nome=$name;comando=($tool+' '+($toolArguments -join ' '));inicioUtc=$started;exitCode=$code}
    if ($code -ne 0) { throw ('Validação da cópia falhou: '+$name) }
}
$passed = $false
Push-Location $projectRoot
try {
    Invoke-DeliveryCheck 'dotnet-restore' 'dotnet' @('restore','PortalTarefas.sln')
    Invoke-DeliveryCheck 'dotnet-build' 'dotnet' @('build','PortalTarefas.sln','-c','Release','--no-restore')
    Invoke-DeliveryCheck 'dotnet-tool-restore' 'dotnet' @('tool','restore')
    Invoke-DeliveryCheck 'dotnet-unitarios' 'dotnet' @('test','tests/PortalTarefas.Tests/PortalTarefas.Tests.csproj','-c','Release','--no-build','--filter','FullyQualifiedName~DomainTests|FullyQualifiedName~InMemoryTests','--logger','trx;LogFileName=unitarios.trx','--results-directory',$resultsRoot)
    Push-Location (Join-Path $projectRoot 'src/PortalTarefas.Frontend')
    try {
        Invoke-DeliveryCheck 'npm-ci' 'npm.cmd' @('ci')
        Invoke-DeliveryCheck 'frontend-testes' 'npm.cmd' @('test')
        Invoke-DeliveryCheck 'frontend-typecheck' 'npm.cmd' @('run','typecheck')
        Invoke-DeliveryCheck 'frontend-build' 'npm.cmd' @('run','build')
    } finally { Pop-Location }
    Invoke-DeliveryCheck 'documentos' 'python' @('scripts/Validate-Documents.py','--output',(Join-Path $resultsRoot 'documentos.json'))
    $passed = $true
} finally {
    Pop-Location
    [ordered]@{instanteUtc=[DateTimeOffset]::UtcNow.ToString('o');projetoExtraido=$projectRoot;aprovado=$passed;etapas=$steps;integracaoSql='Não repetida na cópia: suíte completa validada nas fontes de origem, com banco exclusivo. Apenas domínio/memória nesta verificação independente de banco.';segredosOuDadosCopiados=$false} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $resultsRoot 'resultado.json') -Encoding UTF8
}
Write-Output 'Cópia extraída: restore/build, ferramenta EF, testes independentes, frontend e documentos aprovados.'
