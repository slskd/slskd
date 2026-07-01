$ErrorActionPreference = 'Stop'

function Show-Help {
  Write-Output 'options:'
  Write-Output '-h, --help          show help'
  Write-Output '--no-prebuild       skip build and test'
  Write-Output '--runtime           a valid RID (https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)'
  Write-Output '--platform          one of: linux/amd64, linux/arm64, linux/arm/v7.  overrides runtime.'
  Write-Output '--version           version for the binary. defaults to current git tag+SHA'
  Write-Output '--output            the output directory.  defaults to ../../dist/<runtime>'
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

$prebuild = $true
$runtime = $null
$platform = $null
$version = $null
$output = $null

for ($i = 0; $i -lt $args.Count; $i++) {
  switch ($args[$i]) {
    { $_ -in @('-h', '--help') } {
      Show-Help
      exit 0
    }
    '--no-prebuild' {
      $prebuild = $false
    }
    '--runtime' {
      $i++
      if ($i -ge $args.Count) {
        throw 'missing value for --runtime'
      }

      $runtime = $args[$i]
    }
    '--platform' {
      $i++
      if ($i -ge $args.Count) {
        throw 'missing value for --platform'
      }

      $platform = $args[$i]
    }
    '--version' {
      $i++
      if ($i -ge $args.Count) {
        throw 'missing value for --version'
      }

      $version = $args[$i]
    }
    '--output' {
      $i++
      if ($i -ge $args.Count) {
        throw 'missing value for --output'
      }

      $output = $args[$i]
    }
    default {
      throw "unknown option: $($args[$i])"
    }
  }
}

if (-not [string]::IsNullOrWhiteSpace($platform)) {
  switch ($platform) {
    'linux/amd64' {
      $runtime = 'linux-x64'
    }
    'linux/arm64' {
      $runtime = 'linux-arm64'
    }
    'linux/arm/v7' {
      $runtime = 'linux-arm'
    }
    default {
      Write-Error 'error: platform must be one of: linux/amd64, linux/arm64, linux/arm/v7'
      exit 1
    }
  }

  Write-Output "`n`tInfo:  runtime overridden by platform option $platform, using $runtime`n"
}

if ([string]::IsNullOrWhiteSpace($version)) {
  $version = Get-DefaultVersion
}

if ([string]::IsNullOrWhiteSpace($runtime)) {
  Write-Error 'error: no runtime specified; provide a valid RID (https://docs.microsoft.com/en-us/dotnet/core/rid-catalog)'
  exit 1
}

$root = Split-Path -Parent $PSScriptRoot
$slskdPath = Join-Path $root 'src\slskd'

if ([string]::IsNullOrWhiteSpace($output)) {
  $output = Join-Path $root "dist\$runtime"
}
elseif (-not [System.IO.Path]::IsPathRooted($output)) {
  $output = Join-Path $slskdPath $output
}

if ($prebuild) {
  Write-Output "`n`tRunning:  bin\build.ps1 --version $version`n"
  & (Join-Path $PSScriptRoot 'build.ps1') --version $version
  if ($LASTEXITCODE -ne 0) {
    throw "build.ps1 exited with code $LASTEXITCODE"
  }
}
else {
  Write-Output "`n`t>>  pre-build skipped`n"
}

Push-Location $slskdPath
try {
  Write-Output "`n`tLocation:  $(Get-Location)`n"

  Write-Output "`n`tRunning:  dotnet publish ... --runtime $runtime -p:Version=$version --output $output`n"

  $legacyDistPath = Join-Path (Join-Path $slskdPath 'dist') $runtime
  if (Test-Path -LiteralPath $legacyDistPath) {
    Get-ChildItem -LiteralPath $legacyDistPath -Force | Remove-Item -Recurse -Force
  }

  Invoke-Native dotnet publish `
    --configuration Release `
    -p:PublishSingleFile=true `
    -p:ReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    "-p:Version=$version" `
    --self-contained `
    --runtime $runtime `
    --output $output
}
finally {
  Pop-Location
}

Push-Location $output
try {
  Write-Output "`n`tLocation:  $(Get-Location)`n"
  Write-Output "`n`tArtifacts:`n"
  Get-ChildItem -Force
}
finally {
  Pop-Location
}

Write-Output "`n`tPublish succeeded!`n"
