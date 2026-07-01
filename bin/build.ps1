$ErrorActionPreference = 'Stop'

function Show-Help {
  Write-Output 'options:'
  Write-Output '-h, --help          show help'
  Write-Output '--web-only          skip dotnet build'
  Write-Output '--dotnet-only       skip web build'
  Write-Output '--skip-tests        skip execution of npm and dotnet tests'
  Write-Output '--version           version for the binary. defaults to current git tag+SHA'
}

function Invoke-Native {
  param(
    [Parameter(Mandatory = $true)]
    [string] $FilePath,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
  )

  & $FilePath @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "$FilePath exited with code $LASTEXITCODE"
  }
}

function Get-DefaultVersion {
  $tag = & git describe --tags --abbrev=0
  if ($LASTEXITCODE -ne 0) {
    throw 'git describe exited with an error'
  }

  $sha = & git rev-parse --short HEAD
  if ($LASTEXITCODE -ne 0) {
    throw 'git rev-parse exited with an error'
  }

  return "$tag+$sha"
}

$webOnly = $false
$dotnetOnly = $false
$skipTests = $false
$version = $null

for ($i = 0; $i -lt $args.Count; $i++) {
  switch ($args[$i]) {
    { $_ -in @('-h', '--help') } {
      Show-Help
      exit 0
    }
    '--web-only' {
      $webOnly = $true
    }
    '--dotnet-only' {
      $dotnetOnly = $true
    }
    '--skip-tests' {
      $skipTests = $true
    }
    '--version' {
      $i++
      if ($i -ge $args.Count) {
        throw 'missing value for --version'
      }

      $version = $args[$i]
    }
    default {
      throw "unknown option: $($args[$i])"
    }
  }
}

if ([string]::IsNullOrWhiteSpace($version)) {
  $version = Get-DefaultVersion
}

$root = Split-Path -Parent $PSScriptRoot
$webPath = Join-Path $root 'src\web'
$slskdPath = Join-Path $root 'src\slskd'

if ($dotnetOnly) {
  Write-Output "`n`t>>  web build skipped`n"
}
else {
  Push-Location $webPath
  try {
    Write-Output "`n`tLocation:  $(Get-Location)`n"

    Write-Output "`n`tRunning:  npm ci`n"
    Invoke-Native npm ci

    if ($skipTests) {
      Write-Output "`n`t>>  web tests skipped`n"
    }
    else {
      Write-Output "`n`tTesting:  npm run test-unattended`n"
      Invoke-Native npm run test-unattended
    }

    Write-Output "`n`tRunning:  npm run build`n"
    Invoke-Native npm run build

    if (-not $webOnly) {
      $wwwrootPath = Join-Path $slskdPath 'wwwroot'
      if (Test-Path -LiteralPath $wwwrootPath) {
        Remove-Item -LiteralPath $wwwrootPath -Recurse -Force
      }

      New-Item -ItemType Directory -Path $wwwrootPath | Out-Null
      New-Item -ItemType File -Path (Join-Path $wwwrootPath '.gitkeep') -Force | Out-Null

      Write-Output "`n`tRunning:  Copy-Item build\* $wwwrootPath\`n"
      Copy-Item -Path (Join-Path (Get-Location) 'build\*') -Destination $wwwrootPath -Recurse -Force
    }
  }
  finally {
    Pop-Location
  }
}

if ($webOnly) {
  Write-Output "`n`t>>  dotnet build skipped`n"
}
else {
  Push-Location $slskdPath
  try {
    Write-Output "`n`tLocation:  $(Get-Location)`n"

    Write-Output "`n`tRunning:  dotnet build --no-incremental --nologo --configuration Release -p:Version=$version`n"
    Invoke-Native dotnet build --no-incremental --nologo --configuration Release "-p:Version=$version"

    if ($skipTests) {
      Write-Output "`n`t>>  dotnet tests skipped`n"
    }
    else {
      Write-Output "`n`tTesting:  dotnet test --configuration Release ..\..\tests\slskd.Tests.Unit`n"
      Invoke-Native dotnet test --configuration Release '..\..\tests\slskd.Tests.Unit'
    }
  }
  finally {
    Pop-Location
  }
}

Write-Output "`n`tBuild succeeded!`n"
