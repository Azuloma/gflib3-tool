# Architecture

## Solution

`GFlib3.slnx` contains:

| Project | Path | Role |
| --- | --- | --- |
| `Nxa.GFlib3.Core` | `core/Nxa.GFlib3.Core` | Format readers, containers, shared models |
| `Nxa.GFlib3.Cli` | `core/Nxa.GFlib3.Cli` | Command-line inspection and extraction |
| `Nxa.GFlib3.Core.Tests` | `tests/Nxa.GFlib3.Core.Tests` | Unit and fixture tests |
| `Nxa.GFlib3.Native` | `core/Nxa.GFlib3.Native` | C ABI shim over the vendored `ooz` Kraken decoder (CMake) |
| `Nxa.GFlib3.Render` | `render/Nxa.GFlib3.Render` | Desktop viewer/editor using Silk.NET + ImGui.NET |

All managed projects target `net9.0`. Build settings are centralized in
`Directory.Build.props`; formatting rules live in `.editorconfig`.

## Dependency direction

```text
render  ──► core
cli     ──► core
tests   ──► core
core    ──► native (P/Invoke), third_party/ooz (vendored source)
```

Parsing lives in `core/`; CLI and UI must not duplicate format logic.

## Native components

`third_party/ooz` (GPLv3) provides the Oodle Kraken decompressor. It is compiled
into a small C ABI DLL exposing a single decompress entry point, which
`Nxa.GFlib3.Core` calls through P/Invoke. The proprietary Oodle runtime is never
required.

## Build

```powershell
# Managed projects
dotnet build GFlib3.slnx
dotnet test GFlib3.slnx

# Native Kraken decoder (MSVC, CMake)
pwsh scripts/build-native.ps1
```

`scripts/build-native.ps1` builds `Nxa.GFlib3.Native.dll` from
`core/Nxa.GFlib3.Native` and the vendored `ooz` sources, then copies it to
`artifacts/native/`. The managed loader (`OodleKraken`) resolves the DLL from:

1. the `NXA_GF3_NATIVE` environment variable,
2. the application directory,
3. an `artifacts/native/` directory found by walking up from the application
   directory.

All native output stays under `artifacts/`, which is gitignored.

## Conventions

- Source files start with `SPDX-License-Identifier: GPL-3.0-or-later` and the
  NXAevora copyright line.
- Text files are UTF-8 with LF endings.
- A format is documented in `docs/` before its implementation in `core/`.
- Code quality, ownership, error-context, and comment rules are defined in
  [code-quality.md](code-quality.md).
