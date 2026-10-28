[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [uri]$BaseUrl = 'https://localhost:8443',
    [string]$VersaoEsperada,
    [ValidateRange(1, 60)]
    [int]$Tentativas = 12,
    [switch]$IgnorarCertificadoLocal
)

$ErrorActionPreference = 'Stop'
if ($BaseUrl.Scheme -ne 'https') { throw 'Use uma URL HTTPS para validar a entrega.' }
if ($IgnorarCertificadoLocal -and ($BaseUrl.Host -notin @('localhost', '127.0.0.1', '::1'))) {
    throw 'A exceção de certificado só é permitida para localhost.'
}
if ($IgnorarCertificadoLocal -and $PSVersionTable.PSVersion.Major -lt 7) {
    throw 'A exceção de certificado local exige PowerShell 7 (pwsh). Prefira confiar no certificado.'
}

$opcoes = @{ UseBasicParsing = $true; TimeoutSec = 10; MaximumRedirection = 3 }
if ($IgnorarCertificadoLocal) { $opcoes['SkipCertificateCheck'] = $true }
$raiz = $BaseUrl.AbsoluteUri.TrimEnd('/')
$pronto = $false
for ($tentativa = 1; $tentativa -le $Tentativas; $tentativa++) {
    try {
        $live = Invoke-WebRequest -Uri "$raiz/health/live" @opcoes
        $ready = Invoke-WebRequest -Uri "$raiz/health/ready" @opcoes
        if ($live.StatusCode -eq 200 -and $ready.StatusCode -eq 200) { $pronto = $true; break }
    }
    catch {
        if ($tentativa -eq $Tentativas) { break }
    }
    Start-Sleep -Seconds 2
}
if (-not $pronto) { throw 'Smoke falhou: liveness/readiness não ficaram saudáveis. Consulte os logs locais sem divulgar segredos.' }

$versaoResposta = Invoke-WebRequest -Uri "$raiz/version" @opcoes
$versao = $versaoResposta.Content | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($versao.version)) { throw 'O endpoint /version não informou version.' }
if ($VersaoEsperada -and $versao.version -ne $VersaoEsperada) {
    throw 'A versão respondida não corresponde ao artefato esperado.'
}
$mvc = Invoke-WebRequest -Uri "$raiz/Tarefas/Index" @opcoes
if ($mvc.StatusCode -ne 200 -or $mvc.Content -notmatch 'Portal de Tarefas') { throw 'Smoke falhou na interface MVC.' }
$spa = Invoke-WebRequest -Uri "$raiz/app/" @opcoes
if ($spa.StatusCode -ne 200 -or $spa.Content -notmatch 'id="root"') { throw 'Smoke falhou na entrada da SPA.' }

[pscustomobject]@{
    Resultado = 'Aprovado'
    Endereco = $raiz
    Versao = $versao.version
    Liveness = $live.StatusCode
    Readiness = $ready.StatusCode
    MVC = $mvc.StatusCode
    SPA = $spa.StatusCode
    InstanteUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
