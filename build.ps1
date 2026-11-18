param(
    [string]$TestFilter = "",
    [switch]$Dist
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

$csc = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "No se encuentra csc.exe: $csc" }

# --- Referencias .NET 3.5 (solo para compilar) ---
$refDir = Join-Path $root 'tools\refasm'
$refDll = Join-Path $refDir 'build\.NETFramework\v3.5'
if (-not (Test-Path (Join-Path $refDll 'mscorlib.dll'))) {
    $url = 'https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net35'
    Write-Host "Descargando referencias .NET 3.5 de $url ..."
    New-Item -ItemType Directory -Force $refDir | Out-Null
    $zip = Join-Path $refDir 'refasm.zip'
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    } catch {
        throw "No se pudo descargar el paquete NuGet de referencias .NET 3.5 ($url): $_"
    }
    Expand-Archive -Path $zip -DestinationPath $refDir -Force
    Remove-Item $zip
    if (-not (Test-Path (Join-Path $refDll 'mscorlib.dll'))) { throw "El paquete no contiene $refDll" }
}
$refs = @('mscorlib','System','System.Core','System.Windows.Forms','System.Drawing') |
    ForEach-Object { "/reference:$(Join-Path $refDll ($_ + '.dll'))" }

function Invoke-Csc($target, $out, $sources, $extra) {
    $cscArgs = @('/nologo', '/nostdlib', '/noconfig', "/target:$target", "/out:$out", '/debug:pdbonly', '/warn:4', '/codepage:65001') + $refs + $extra + $sources
    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de $out" }
}

$out  = Join-Path $root 'out'
$distDir = Join-Path $root 'dist\CopiaSegura'
New-Item -ItemType Directory -Force $out, $distDir | Out-Null
$config = Join-Path $root 'src\CopiaSegura.exe.config'

# --- Aplicacion ---
$appSrc = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object FullName)
Invoke-Csc 'winexe' (Join-Path $distDir 'CopiaSegura.exe') $appSrc @("/win32manifest:$(Join-Path $root 'src\app.manifest')")
Copy-Item $config (Join-Path $distDir 'CopiaSegura.exe.config') -Force

# --- Pruebas ---
$testSrc = @(Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object FullName)
$coreDir = Join-Path $root 'src\Core'
if (Test-Path $coreDir) { $testSrc += @(Get-ChildItem $coreDir -Filter *.cs | ForEach-Object FullName) }
Invoke-Csc 'exe' (Join-Path $out 'Tests.exe') $testSrc @()
Copy-Item $config (Join-Path $out 'Tests.exe.config') -Force

& (Join-Path $out 'Tests.exe') $TestFilter
$testExit = $LASTEXITCODE
if ($testExit -ne 0) { Write-Host "PRUEBAS FALLIDAS"; exit $testExit }

# --- Prueba de humo: recorre los 5 pasos con una copia real pequena ---
if ($TestFilter -eq "") {
    $smoke = Start-Process -FilePath (Join-Path $distDir 'CopiaSegura.exe') -ArgumentList '--smoke' -Wait -PassThru -RedirectStandardError (Join-Path $out 'smoke_err.txt')
    if ($smoke.ExitCode -ne 0) {
        Get-Content (Join-Path $out 'smoke_err.txt') | Select-Object -First 20
        Write-Host "PRUEBA DE HUMO FALLIDA"
        exit 1
    }
    Write-Host "SMOKE OK"
}

if ($Dist) {
    # Carpeta portable limpia: solo exe + config + LEEME (sin pdb ni datos de prueba)
    $pkg = Join-Path $root 'dist\pkg\CopiaSegura'
    if (Test-Path (Join-Path $root 'dist\pkg')) { Remove-Item (Join-Path $root 'dist\pkg') -Recurse -Force }
    New-Item -ItemType Directory -Force $pkg | Out-Null
    Copy-Item (Join-Path $distDir 'CopiaSegura.exe') $pkg
    Copy-Item (Join-Path $distDir 'CopiaSegura.exe.config') $pkg
    Copy-Item (Join-Path $root 'src\LEEME.txt') $pkg
    $zip = Join-Path $root 'dist\CopiaSegura.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $pkg -DestinationPath $zip
    $size = (Get-Item $zip).Length
    Write-Host ("DIST OK: {0} ({1:N0} bytes)" -f $zip, $size)
}
exit 0
