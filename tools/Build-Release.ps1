param([string]$Version = '1.2.0')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
 if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a semantic version, for example 1.2.0.' }
 dotnet run --project tests/MedikitHome.Tests -c Release -- $projectRoot
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
 $publish = Join-Path $projectRoot "artifacts/publish-$Version"
 if (Test-Path -LiteralPath $publish) { throw "Output already exists: $publish. Use a new version or remove that build folder first." }
 dotnet publish src/HomePharma -c Release -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true "-p:ApplicationDisplayVersion=$Version" -o $publish
 if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
 foreach ($file in @('MedikitHome.exe','MedikitHome.dll','HomePharma.Core.dll','coreclr.dll','e_sqlite3.dll','WebView2Loader.dll','medicamentos.csv')) {
  if (!(Test-Path -LiteralPath (Join-Path $publish $file))) { throw "Missing runtime file: $file" }
 }
 Copy-Item -LiteralPath README.md -Destination (Join-Path $publish 'README.md')
 Copy-Item -LiteralPath LICENSE -Destination (Join-Path $publish 'LICENSE')
 Copy-Item -LiteralPath THIRD_PARTY_NOTICES.md -Destination $publish
 Copy-Item -LiteralPath licenses -Destination $publish -Recurse
 $zip = Join-Path $projectRoot "artifacts/MedikitHome-$Version-Windows-x64.zip"
 Compress-Archive -Path "$publish/*" -DestinationPath $zip
 $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
 "$hash  $(Split-Path $zip -Leaf)" | Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/SHA256SUMS.txt') -Encoding ascii
 Write-Host "Release ready: $zip"
} finally { Pop-Location }
