# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright 2026 the NXAevora Projects

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'core/Nxa.GFlib3.Native'
$build = Join-Path $root 'artifacts/native-build'
$output = Join-Path $root 'artifacts/native'

cmake -S $source -B $build -G 'Visual Studio 18 2026' -A x64
cmake --build $build --config $Configuration

New-Item -ItemType Directory -Force $output | Out-Null
$dll = Join-Path $build "bin/$Configuration/Nxa.GFlib3.Native.dll"
Copy-Item $dll $output -Force

Write-Output "Built $dll"
Write-Output "Copied to $output"
