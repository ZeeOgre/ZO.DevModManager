# DevModManager Dependency Checker — DMMDeps

DMMDeps is a dependency discovery and archive-list generation tool for Starfield mods.

It scans a Starfield plugin and follows its referenced assets to produce complete, reviewable file lists for packaging and archive creation. It is designed to help mod authors identify the meshes, materials, textures, scripts, terrain data, interface assets, voices, animations, and other resources required by a plugin.

## Download

Download the latest release here:

**[Download the latest DMMDeps release](https://github.com/ZeeOgre/DevModManager/releases/latest)**

Two Windows x64 builds are provided:

- **`dmmdeps.zip`** — Slim, framework-dependent single-executable build. Requires the .NET 10 Runtime to be installed.
- **`dmmdeps-fat.zip`** — Self-contained single-executable build. Includes the required .NET 10 runtime and does not require a separate .NET installation.

Extract the selected ZIP and run `dmmdeps.exe`.

## What DMMDeps Does

DMMDeps scans Starfield `.esm` and `.esp` plugins and discovers referenced assets including:

- Meshes and external geometry
- Materials and textures
- PC, Xbox, and PS5 asset paths where applicable
- Papyrus scripts and imported script dependencies
- Terrain data, biome data, overlay masks, and source TIF files
- Inventory, Ship Builder, and Workshop interface icons
- Voice assets
- Animations, behaviors, morphs, rigs, particles, and Havok assets
- Additional plugin-defined resources recognized by the dependency readers

NIF dependency discovery uses structured Bethesda-family parsing and typed Starfield block fields where available, with diagnostic fallback handling for incomplete or unsupported files.

## Basic Usage

```powershell
dmmdeps.exe "C:\Path\To\Starfield\Data\YourMod.esm"
```

DMMDeps writes its output files beside the plugin.

Example:

```text
YourMod.achlist
YourMod.achlist_warn
YourMod.achlist_discard
YourMod_deps.csv
YourMod_deps.json
```

## Output Files

### `.achlist`

The primary asset list.

It contains verified PC file paths that should normally be included in the mod package or passed to the archive-management workflow.

### `.achlist_warn`

Assets that were discovered but also exist in an allowed parent archive.

These entries may be valid parent dependencies, intentionally overridden files, or items that require manual review.

### `.achlist_discard`

High-probability false positives or invalid paths discovered while decoding source assets.

These entries are normally excluded from packaging but are retained for review and troubleshooting.

### `_deps.csv`

A detailed spreadsheet-friendly dependency report containing file paths, asset types, platform paths, discovery sources, and related metadata.

### `_deps.json`

The full dependency manifest in JSON format.

This file is omitted when using `--silent`.

## Common Examples

### Automatic discovery

```powershell
dmmdeps.exe "C:\Games\Starfield\Data\YourMod.esm"
```

### Custom development layout

```powershell
dmmdeps.exe "G:\ModDev\YourMod.esm" --gameroot "C:\Games\Starfield" --tifroot "G:\Source\TGATextures"
```

### Include Papyrus source files

```powershell
dmmdeps.exe "C:\Games\Starfield\Data\YourMod.esm" --include-psc
```

### Preserve parent `.mat` files

```powershell
dmmdeps.exe "C:\Games\Starfield\Data\YourMod.esm" --preserve-parent-mat
```

This keeps matching parent-archive material files in the generated asset list without recursively packaging the textures referenced by those parent materials.

### Seed from an existing `.achlist`

```powershell
dmmdeps.exe "C:\Games\Starfield\Data\YourMod.esm" --smartclobber
```

This preserves useful manually added entries from an existing asset list while rebuilding discovered dependencies.

### Force a parent-archive cache rebuild

```powershell
dmmdeps.exe "C:\Games\Starfield\Data\YourMod.esm" --rebuildcache
```

This is intended for troubleshooting. DMMDeps normally detects parent-archive changes automatically.

### Quiet CI/CD execution

```powershell
dmmdeps.exe "C:\Games\Starfield\Data\YourMod.esm" --silent
```

## Command-Line Options

```text
dmmdeps.exe <pluginPath> [options]

Options:
  --gameroot <path>        Override the game root directory
  --xboxdata <path>        Override the Xbox Data root
  --ps5data <path>         Override the PS5 Data root
  --tifroot <path>         Override the source TIF root
  --scriptsroot <path>     Override the Scripts directory
  --test                   Write a test achlist instead of replacing the normal output
  --quiet                  Suppress routine skipped-file messages
  --silent                 Suppress output except startup and completion information
  --verbose                Emit detailed dependency and diagnostic information
  --smartclobber           Seed candidates from an existing achlist
  --rebuildcache           Force rebuilding the parent-archive cache
  --include-psc            Include Papyrus source files
  --preserve-parent-mat    Keep matching parent material files without walking their textures
```

Run the executable without arguments to see the authoritative option list for the installed version.

## Parent-Archive Cache

DMMDeps maintains a local SQLite index of files found in allowed parent BA2 and ZIP archives.

The cache is stored beneath:

```text
%LOCALAPPDATA%\ZeeOgre\dmmdeps\
```

Cache validation uses a two-stage process:

1. File length and UTC last-write metadata are checked first.
2. Same-length archives with changed timestamps are verified using XXH128 content fingerprints.

Unchanged archives normally require no archive read or content hash. Changed archives are rescanned and their stored fingerprint is refreshed.

Use `--rebuildcache` only when troubleshooting or when a manual rebuild is specifically desired.

## Archive Manager Wrapper

The repository also includes a PowerShell-based Starfield Archive Manager workflow that can:

- Run DMMDeps
- Generate archive lists
- Build PC and Xbox BA2 archives
- Preserve selected parent material files
- Force rebuilding the parent-archive cache
- Manage archive-related backup and publishing operations

The wrapper exposes the commonly used DMMDeps settings through its Archive Management interface.

## Requirements

### Slim build

The slim `dmmdeps.zip` build requires:

- Windows x64
- .NET 10 Runtime
- Starfield or a configured Starfield development layout

### Self-contained build

The `dmmdeps-fat.zip` build requires:

- Windows x64
- No separate .NET installation

## Release Notes

Version-specific changes are maintained in:

[`CHANGELOG.md`](CHANGELOG.md)

The latest packaged release and its release notes are always available here:

**[Latest DevModManager release](https://github.com/ZeeOgre/DevModManager/releases/latest)**

## License

DevModManager and DMMDeps are licensed under the GNU General Public License v3.0 only.

See the repository license and `THIRD_PARTY_NOTICES.md` for full licensing and attribution information.
