[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$Initialize,
    [string]$ResultsDirectory = 'artifacts/tests'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# A conexão fica no ambiente, nunca nos argumentos de processo ou no relatório.
if ([string]::IsNullOrWhiteSpace($env:PORTAL_TEST_CONNECTION)) {
    throw 'Defina PORTAL_TEST_CONNECTION para um banco SQL Server exclusivo de testes.'
}
$connectionBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
try { $connectionBuilder.set_ConnectionString($env:PORTAL_TEST_CONNECTION) }
catch { throw 'PORTAL_TEST_CONNECTION não é uma conexão válida. O valor foi omitido.' }
$databaseKeys = @(@('Initial Catalog', 'Database') | Where-Object { $connectionBuilder.ContainsKey($_) })
if ($databaseKeys.Count -ne 1) {
    throw 'Informe exatamente uma chave Database ou Initial Catalog na conexão de testes.'
}
$testDatabase = [string]$connectionBuilder[$databaseKeys[0]]
if ($testDatabase -cnotmatch '^PortalTarefas_Tests_[A-Za-z0-9_]+$') {
    throw 'O banco deve ter o nome PortalTarefas_Tests_<sufixo>. A conexão foi recusada antes de executar comandos.'
}
if ($connectionBuilder.ContainsKey('AttachDBFilename') -or $connectionBuilder.ContainsKey('AttachDbFileName')) {
    throw 'A conexão de testes não pode anexar arquivos de banco.'
}

$solutionRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $solutionRoot 'tests/PortalTarefas.Tests/PortalTarefas.Tests.csproj'
$webProject = Join-Path $solutionRoot 'src/PortalTarefas.Web/PortalTarefas.Web.csproj'
if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $resultsRoot = $ResultsDirectory }
else { $resultsRoot = Join-Path $solutionRoot $ResultsDirectory }
# Um diretório por execução impede que um TRX antigo seja confundido com evidência nova.
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runDirectory = Join-Path $resultsRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

Push-Location $solutionRoot
try {
    if ($Initialize) {
        $previousConnection = $env:ConnectionStrings__PortalTarefas
        $previousStorage = $env:Portal__Storage
        $previousSeed = $env:Seed__Enabled
        $previousSamples = $env:Seed__SampleData
        $previousAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $previousDotNetEnvironment = $env:DOTNET_ENVIRONMENT
        $previousAdminEmail = $env:Seed__AdminEmail
        $previousAdminPassword = $env:Seed__AdminPassword
        $previousUserEmail = $env:Seed__UserEmail
        $previousUserPassword = $env:Seed__UserPassword
        try {
            $env:ConnectionStrings__PortalTarefas = $env:PORTAL_TEST_CONNECTION
            $env:Portal__Storage = 'SqlServer'
            $env:Seed__Enabled = 'false'
            $env:Seed__SampleData = 'false'
            # Testing impede carregar User Secrets e configurações Development.
            # Os dois nomes evitam depender da precedência do host .NET.
            $env:ASPNETCORE_ENVIRONMENT = 'Testing'
            $env:DOTNET_ENVIRONMENT = 'Testing'
            $env:Seed__AdminEmail = ''
            $env:Seed__AdminPassword = ''
            $env:Seed__UserEmail = ''
            $env:Seed__UserPassword = ''
            $initializeArguments = @('run', '--project', $webProject, '-c', $Configuration, '--no-launch-profile')
            if ($NoBuild) { $initializeArguments += '--no-build' }
            & dotnet @initializeArguments -- --initialize
            if ($LASTEXITCODE -ne 0) { throw 'Falha ao aplicar migrations no banco exclusivo de testes.' }
        }
        finally {
            $env:ConnectionStrings__PortalTarefas = $previousConnection
            $env:Portal__Storage = $previousStorage
            $env:Seed__Enabled = $previousSeed
            $env:Seed__SampleData = $previousSamples
            $env:ASPNETCORE_ENVIRONMENT = $previousAspNetEnvironment
            $env:DOTNET_ENVIRONMENT = $previousDotNetEnvironment
            $env:Seed__AdminEmail = $previousAdminEmail
            $env:Seed__AdminPassword = $previousAdminPassword
            $env:Seed__UserEmail = $previousUserEmail
            $env:Seed__UserPassword = $previousUserPassword
        }
    }

    $testArguments = @('test', $project, '-c', $Configuration, '--logger', 'trx;LogFileName=integration.trx', '--results-directory', $runDirectory)
    if ($NoBuild) { $testArguments += '--no-build' }
    & dotnet @testArguments
    $testExitCode = $LASTEXITCODE
    $trxPath = Join-Path $runDirectory 'integration.trx'
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw 'A execução não produziu um TRX novo; os testes não foram comprovados.'
    }

    [xml]$report = Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8
    $results = @($report.SelectNodes("//*[local-name()='UnitTestResult']"))
    $passed = @($results | Where-Object { $_.outcome -eq 'Passed' }).Count
    $skipped = @($results | Where-Object { $_.outcome -eq 'NotExecuted' }).Count
    $failed = @($results | Where-Object { $_.outcome -notin @('Passed', 'NotExecuted') }).Count
    $methods = @($report.SelectNodes("//*[local-name()='UnitTest']/*[local-name()='TestMethod']"))
    $missingSuites = @(@('ApiTests', 'SqlIntegrationTests', 'ObservabilidadeTests') | Where-Object {
        $suite = $_
        @($methods | Where-Object { $_.className -match ('\.' + [Regex]::Escape($suite) + '(,|$)') }).Count -eq 0
    })
    $summary = [ordered]@{
        instanteUtc = [DateTimeOffset]::UtcNow.ToString('O')
        banco = $testDatabase
        total = $results.Count
        aprovados = $passed
        falhos = $failed
        ignorados = $skipped
        suitesAusentes = $missingSuites
        exitCode = $testExitCode
        trx = 'integration.trx'
    }
    $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding UTF8
    Write-Host "Testes: $passed aprovados, $failed falhos, $skipped ignorados. Evidência: $runDirectory"
    if ($testExitCode -ne 0 -or $failed -gt 0 -or $skipped -gt 0 -or $results.Count -eq 0 -or $missingSuites.Count -gt 0) {
        throw 'Validação incompleta: nenhuma suíte obrigatória pode faltar, falhar ou ser ignorada. Consulte o TRX e summary.json.'
    }
}
finally { Pop-Location }
