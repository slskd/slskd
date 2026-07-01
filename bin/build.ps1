[CmdletBinding()]
param(
  [Alias('h')]
  [switch] $Help,
  [switch] $WebOnly,
  [switch] $DotnetOnly,
  [switch] $SkipTests,
  [string] $Version
)

$ErrorActionPreference = 'Stop'

function Show-Help {
  Write-Output 'options:'
  Write-Output '-Help              show help'
  Write-Output '-WebOnly           skip dotnet build'
  Write-Output '-DotnetOnly        skip web build'
  Write-Output '-SkipTests         skip execution of npm and dotnet tests'
  Write-Output '-Version           version for the binary. defaults to current git tag+SHA'
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

if ($Help) {
  Show-Help
  exit 0
}

if ([string]::IsNullOrWhiteSpace($Version)) {
  $Version = Get-DefaultVersion
}

$root = Split-Path -Parent $PSScriptRoot
$webPath = Join-Path $root 'src\web'
$slskdPath = Join-Path $root 'src\slskd'

if ($DotnetOnly) {
  Write-Output ">>  web build skipped"
}
else {
  Push-Location $webPath
  try {
    Write-Output "Location:  $(Get-Location)"

    Write-Output "Running:  npm ci"
    Invoke-Native npm ci

    if ($SkipTests) {
      Write-Output ">>  web tests skipped"
    }
    else {
      Write-Output "Testing:  npm run test-unattended"
      Invoke-Native npm run test-unattended
    }

    Write-Output "Running:  npm run build"
    Invoke-Native npm run build

    if (-not $WebOnly) {
      $wwwrootPath = Join-Path $slskdPath 'wwwroot'
      if (Test-Path -LiteralPath $wwwrootPath) {
        Remove-Item -LiteralPath $wwwrootPath -Recurse -Force
      }

      New-Item -ItemType Directory -Path $wwwrootPath | Out-Null
      New-Item -ItemType File -Path (Join-Path $wwwrootPath '.gitkeep') -Force | Out-Null

      Write-Output "Running:  Copy-Item build\* $wwwrootPath\"
      Copy-Item -Path (Join-Path (Get-Location) 'build\*') -Destination $wwwrootPath -Recurse -Force
    }
  }
  finally {
    Pop-Location
  }
}

if ($WebOnly) {
  Write-Output ">>  dotnet build skipped"
}
else {
  Push-Location $slskdPath
  try {
    Write-Output "Location:  $(Get-Location)"

    Write-Output "Running:  dotnet build --no-incremental --nologo --configuration Release -p:Version=$Version"
    Invoke-Native dotnet build --no-incremental --nologo --configuration Release "-p:Version=$Version"

    if ($SkipTests) {
      Write-Output ">>  dotnet tests skipped"
    }
    else {
      Write-Output "Testing:  dotnet test --configuration Release ..\..\tests\slskd.Tests.Unit"
      Invoke-Native dotnet test --configuration Release '..\..\tests\slskd.Tests.Unit'
    }
  }
  finally {
    Pop-Location
  }
}

Write-Output "Build succeeded!"
