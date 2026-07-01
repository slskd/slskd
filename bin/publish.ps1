[CmdletBinding()]
param(
  [Alias('h')]
  [switch] $Help,
  [switch] $NoPrebuild,
  [string] $Runtime,
  [string] $Platform,
  [string] $Version,
  [string] $Output
)

$ErrorActionPreference = 'Stop'

function Show-Help {
  Write-Output 'options:'
  Write-Output '-Help              show help'
  Write-Output '-NoPrebuild        skip build and test'
  Write-Output '-Runtime           a valid RID (https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)'
  Write-Output '-Platform          one of: linux/amd64, linux/arm64, linux/arm/v7.  overrides runtime.'
  Write-Output '-Version           version for the binary. defaults to current git tag+SHA'
  Write-Output '-Output            the output directory.  defaults to ../../dist/<runtime>'
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

if (-not [string]::IsNullOrWhiteSpace($Platform)) {
  switch ($Platform) {
    'linux/amd64' {
      $Runtime = 'linux-x64'
    }
    'linux/arm64' {
      $Runtime = 'linux-arm64'
    }
    'linux/arm/v7' {
      $Runtime = 'linux-arm'
    }
    default {
      Write-Error 'error: platform must be one of: linux/amd64, linux/arm64, linux/arm/v7'
      exit 1
    }
  }

  Write-Output "Info:  runtime overridden by platform option $Platform, using $Runtime"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
  $Version = Get-DefaultVersion
}

if ([string]::IsNullOrWhiteSpace($Runtime)) {
  Write-Error 'error: no runtime specified; provide a valid RID (https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)'
  exit 1
}

$root = Split-Path -Parent $PSScriptRoot
$slskdPath = Join-Path $root 'src\slskd'

if ([string]::IsNullOrWhiteSpace($Output)) {
  $Output = Join-Path $root "dist\$Runtime"
}
elseif (-not [System.IO.Path]::IsPathRooted($Output)) {
  $Output = Join-Path $slskdPath $Output
}

if (-not $NoPrebuild) {
  Write-Output "Running:  bin\build.ps1 -Version $Version"
  & (Join-Path $PSScriptRoot 'build.ps1') -Version $Version
  if ($LASTEXITCODE -ne 0) {
    throw "build.ps1 exited with code $LASTEXITCODE"
  }
}
else {
  Write-Output ">>  pre-build skipped"
}

Push-Location $slskdPath
try {
  Write-Output "Location:  $(Get-Location)"

  Write-Output "Running:  dotnet publish ... --runtime $Runtime -p:Version=$Version --output $Output"

  $legacyDistPath = Join-Path (Join-Path $slskdPath 'dist') $Runtime
  if (Test-Path -LiteralPath $legacyDistPath) {
    Get-ChildItem -LiteralPath $legacyDistPath -Force | Remove-Item -Recurse -Force
  }

  Invoke-Native dotnet publish `
    --configuration Release `
    "-p:PublishSingleFile=true" `
    "-p:ReadyToRun=true" `
    "-p:IncludeNativeLibrariesForSelfExtract=true" `
    "-p:CopyOutputSymbolsToPublishDirectory=false" `
    "-p:Version=$Version" `
    --self-contained `
    --runtime $Runtime `
    --output $Output
}
finally {
  Pop-Location
}

Push-Location $Output
try {
  Write-Output "Location:  $(Get-Location)"
  Write-Output "Artifacts:"
  Get-ChildItem -Force
}
finally {
  Pop-Location
}

Write-Output "Publish succeeded!"
