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
if ($BaseUrl.UserInfo) { throw 'A URL do smoke nao pode conter credenciais.' }
if ($IgnorarCertificadoLocal -and ($BaseUrl.Host -notin @('localhost', '127.0.0.1', '::1'))) {
    throw 'A exceção de certificado só é permitida para localhost.'
}
$opcoes = @{ UseBasicParsing = $true; TimeoutSec = 10; MaximumRedirection = 3 }
if ($IgnorarCertificadoLocal -and $PSVersionTable.PSVersion.Major -ge 7) { $opcoes['SkipCertificateCheck'] = $true }

function Invoke-PortalSmokeRequest([string]$Uri) {
    if ($IgnorarCertificadoLocal -and $PSVersionTable.PSVersion.Major -lt 7) {
        # Windows PowerShell 5 não suporta SkipCertificateCheck. A exceção fica
        # limitada a esta chamada curl ao localhost, sem alterar validação TLS global.
        $curlCommand = Get-Command curl.exe -ErrorAction Stop
        $responseFile = [IO.Path]::GetTempFileName()
        try {
            $status = & $curlCommand.Source --silent --show-error --fail --insecure --max-time 10 `
                --output $responseFile --write-out '%{http_code}' $Uri
            if ($LASTEXITCODE -ne 0) { throw 'A requisição HTTPS local falhou.' }
            return [pscustomobject]@{ StatusCode = [int]$status; Content = [IO.File]::ReadAllText($responseFile) }
        }
        finally { Remove-Item -LiteralPath $responseFile -Force }
    }
    return Invoke-WebRequest -Uri $Uri @opcoes
}
$raiz = $BaseUrl.AbsoluteUri.TrimEnd('/')
$pronto = $false
for ($tentativa = 1; $tentativa -le $Tentativas; $tentativa++) {
    try {
        $live = Invoke-PortalSmokeRequest "$raiz/health/live"
        $ready = Invoke-PortalSmokeRequest "$raiz/health/ready"
        if ($live.StatusCode -eq 200 -and $ready.StatusCode -eq 200) { $pronto = $true; break }
    }
    catch {
        if ($tentativa -eq $Tentativas) { break }
    }
    Start-Sleep -Seconds 2
}
if (-not $pronto) { throw 'Smoke falhou: liveness/readiness não ficaram saudáveis. Consulte os logs locais sem divulgar segredos.' }

$versaoResposta = Invoke-PortalSmokeRequest "$raiz/version"
if ($versaoResposta.StatusCode -ne 200) { throw 'Smoke falhou no endpoint de versao.' }
$versao = $versaoResposta.Content | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($versao.version)) { throw 'O endpoint /version não informou version.' }
if ($VersaoEsperada -and $versao.version -ne $VersaoEsperada) {
    throw 'A versão respondida não corresponde ao artefato esperado.'
}
$mvc = Invoke-PortalSmokeRequest "$raiz/Tarefas/Index"
if ($mvc.StatusCode -ne 200 -or $mvc.Content -notmatch 'Portal de Tarefas') { throw 'Smoke falhou na interface MVC.' }
$spa = Invoke-PortalSmokeRequest "$raiz/app/"
if ($spa.StatusCode -ne 200 -or $spa.Content -notmatch 'id="root"') { throw 'Smoke falhou na entrada da SPA.' }
$assets = @([Regex]::Matches($spa.Content, '(?:src|href)="(?<url>/app/assets/[^"?#]+\.(?:js|css))"') | ForEach-Object {
    $_.Groups['url'].Value
} | Select-Object -Unique)
if (@($assets | Where-Object { $_ -like '*.js' }).Count -eq 0 -or @($assets | Where-Object { $_ -like '*.css' }).Count -eq 0) {
    throw 'Smoke falhou: a SPA precisa referenciar seus assets JavaScript e CSS.'
}
foreach ($asset in $assets) {
    $respostaAsset = Invoke-PortalSmokeRequest ($BaseUrl.GetLeftPart([UriPartial]::Authority) + $asset)
    if ($respostaAsset.StatusCode -ne 200 -or [string]::IsNullOrWhiteSpace($respostaAsset.Content) -or $respostaAsset.Content -match '^\s*<!doctype html') {
        throw 'Smoke falhou: um asset JavaScript/CSS da SPA nao foi servido corretamente.'
    }
}

[pscustomobject]@{
    Resultado = 'Aprovado'
    Endereco = $raiz
    Versao = $versao.version
    Liveness = $live.StatusCode
    Readiness = $ready.StatusCode
    MVC = $mvc.StatusCode
    SPA = $spa.StatusCode
    Assets = $assets.Count
    InstanteUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
