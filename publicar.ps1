# Publica la descarga: compila, genera el zip y actualiza la rama "gh-pages"
# (pagina web + CopiaSegura.zip), que GitHub Pages sirve como sitio de descarga.
# Uso:  ./publicar.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') fallo (codigo $LASTEXITCODE)" }
}

# 1) Que no haya cambios sin guardar en lo que se va a publicar
$dirty = & git status --porcelain
if ($dirty) { Write-Warning "Hay cambios sin commitear en el repositorio; la web se publica con lo compilado ahora." }

# 2) Compilar, probar y empaquetar
& (Join-Path $root 'build.ps1') -Dist
if ($LASTEXITCODE -ne 0) { throw "build.ps1 fallo: no se publica nada." }
$zip = Join-Path $root 'dist\CopiaSegura.zip'
if (-not (Test-Path $zip)) { throw "No existe $zip" }

# 3) Datos para la plantilla
$sha  = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$size = '{0:N0} KB' -f [math]::Ceiling((Get-Item $zip).Length / 1KB)
$date = Get-Date -Format 'dd/MM/yyyy'
$html = (Get-Content (Join-Path $root 'site\index.html') -Raw -Encoding UTF8).
    Replace('{{FECHA}}', $date).Replace('{{TAMANO}}', $size).Replace('{{SHA256}}', $sha)

# 4) Preparar la rama gh-pages en una carpeta temporal (no toca tu carpeta de trabajo)
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('cs_pages_' + [guid]::NewGuid().ToString('N'))
$remoteHas = ((& git ls-remote --heads origin gh-pages) | Measure-Object).Count -gt 0
try {
    if ($remoteHas) {
        Invoke-Git fetch origin gh-pages
        Invoke-Git worktree add -B gh-pages $tmp origin/gh-pages
    } else {
        Invoke-Git worktree add --orphan -b gh-pages $tmp
    }
    Get-ChildItem $tmp -Force | Where-Object { $_.Name -ne '.git' } | Remove-Item -Recurse -Force
    # Recursos estaticos de la web (logo, capturas...) salvo la plantilla y el generador de iconos
    Get-ChildItem (Join-Path $root 'site') | Where-Object { $_.Name -notin @('index.html', 'generar-iconos.ps1') } |
        ForEach-Object { Copy-Item $_.FullName $tmp -Recurse -Force }
    [IO.File]::WriteAllText((Join-Path $tmp 'index.html'), $html, (New-Object Text.UTF8Encoding($false)))
    Copy-Item $zip (Join-Path $tmp 'CopiaSegura.zip')
    New-Item -ItemType File (Join-Path $tmp '.nojekyll') | Out-Null

    Push-Location $tmp
    try {
        Invoke-Git add -A
        $changes = & git status --porcelain
        if (-not $changes) {
            Write-Host "La web ya estaba al dia: nada que publicar."
        } else {
            Invoke-Git commit -q -m "Publicar descarga $date (SHA-256 $($sha.Substring(0,12)))"
            Invoke-Git push origin gh-pages
            Write-Host "Publicado. SHA-256: $sha"
        }
    } finally { Pop-Location }
} finally {
    $ErrorActionPreference = 'Continue'
    if (Test-Path $tmp) { & git worktree remove --force $tmp 2>&1 | Out-Null }
    & git worktree prune 2>&1 | Out-Null
}
Write-Host "Publicado en la rama gh-pages (GitHub Pages: Settings > Pages > Branch: gh-pages)."
