# Yeni sürüm yayınlar: sürümü yükseltir, derler, GitHub'a gönderir ve Release oluşturur.
# Kullanım:  .\yayinla.ps1            (sürüm ve notu sorar)
#            .\yayinla.ps1 1.0.1 "Kurulum ekranında düzeltme"
param([string]$Version, [string]$Notes)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csproj = 'src\MetroKafe\MetroKafe.csproj'

$current = ([xml](Get-Content $csproj -Encoding UTF8)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "Mevcut sürüm: $current"

if (-not $Version) {
    $p = $current.Split('.')
    $suggested = "$($p[0]).$($p[1]).$([int]$p[2] + 1)"
    $Version = Read-Host "Yeni sürüm [$suggested]"
    if (-not $Version) { $Version = $suggested }
}
$Version = $Version.TrimStart('v', 'V')
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Sürüm 1.2.3 biçiminde olmalı." }
if ([version]$Version -le [version]$current) { throw "Yeni sürüm ($Version) mevcut sürümden ($current) büyük olmalı." }
if (-not $Notes) { $Notes = Read-Host "Bu sürümde neler değişti" }
if (-not $Notes) { $Notes = "v$Version" }

Write-Host "`nSürüm $Version yayınlanacak."

# Sürüm numarasını güncelle
$text = [IO.File]::ReadAllText($csproj)
$text = $text -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>"
[IO.File]::WriteAllText($csproj, $text, (New-Object Text.UTF8Encoding($false)))

# Derle
dotnet build $csproj -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız." }
New-Item -ItemType Directory -Force dist | Out-Null
Copy-Item 'src\MetroKafe\bin\Release\net48\MetroKafe.exe' 'dist\MetroKafe.exe' -Force

# GitHub
git add -A
git commit -m "v$Version - $Notes"
git tag "v$Version"
git push
git push origin "v$Version"
gh release create "v$Version" 'dist\MetroKafe.exe' --title "v$Version" --notes $Notes
if ($LASTEXITCODE -ne 0) { throw "GitHub Release oluşturulamadı." }

Write-Host "`nTamam: v$Version yayınlandı. Admin paneli > Güncelleme ile alınabilir." -ForegroundColor Green
