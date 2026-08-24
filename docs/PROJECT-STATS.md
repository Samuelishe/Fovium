# ProjectStats

Role: Contract and usage guide for the repository diagnostics CLI.
Read when: Changing ProjectStats, interpreting its report, or collecting repository-level size metrics.
Authoritative for: ProjectStats purpose, commands, exclusions, metric semantics, heuristic limits, and generated-output policy.
Not authoritative for: Code quality, coverage, semantic analysis, build success, or current project state.

## Purpose

`Fovium.Tools.ProjectStats` is a deterministic, cross-platform, BCL-only repository diagnostics CLI. It scans a repository without following reparse-point directory trees and reports compact structural metrics. It does not change source files, execute tests, analyze C# semantics, or act as a quality gate. Only an explicit `--output` request writes a report.

## Usage

```powershell
dotnet run --project Fovium.Tools.ProjectStats -- .
dotnet run --project Fovium.Tools.ProjectStats -- . --top 25
dotnet run --project Fovium.Tools.ProjectStats -- . --markdown
dotnet run --project Fovium.Tools.ProjectStats -- . --json
dotnet run --project Fovium.Tools.ProjectStats -- . --markdown --output project-stats.md
dotnet run --project Fovium.Tools.ProjectStats -- . --json --output project-stats.json
pwsh eng/project-stats.ps1
pwsh eng/project-stats.ps1 -Top 25
```

The repository root is the first positional argument and defaults to the current directory. Relative output paths are resolved beneath the selected root. `--top` must be a positive integer. Markdown and JSON output modes are mutually exclusive; without either, the report is human-readable console text.

## Exclusions

The scanner excludes `.git`, `.idea`, `.vs`, `.vscode`, `bin`, `obj`, `packages`, `artifacts`, `publish`, `TestResults`, and `.codex-cache` directories. It also excludes `*.user`, `*.tmp`, `*.temp`, `*.cache`, the conventional repository-root `project-stats.md/json` reports, and the explicit output target. A same-named file below a source-controlled subdirectory is not mistaken for the generated root report.

Reparse-point files/directories are recorded as skipped and not followed. Individual inaccessible paths are recorded by repository-relative path and failure type while the remaining scan continues.

## Metric semantics

- General counts cover successfully scanned files; text counts cover recognized repository text formats.
- Extension groups are lexical file-extension groups.
- C# files are classified as Production, Tests, or Tooling by stable repository path ownership, not syntax or assembly analysis.
- XAML means `.xaml` files. A future Avalonia `.axaml` policy should be added deliberately when application code exists.
- `[Fact]` and `[Theory]` counts are approximate lexical counts in test-owned C# files, not authoritative test discovery.
- Largest-file lists use character count, then ordinal repository-relative path for ties.
- Folder density covers `.cs`, `.xaml`, and `.md` files and groups by repository-relative containing folder.
- Line and character counts reflect decoded text, not executable statements or complexity.

## Generated output policy

`project-stats.md` and `project-stats.json` are local diagnostics ignored by Git. Do not quote their changing totals into `PROJECT-STATE.md`, use them as CI gates, or treat a larger/smaller number as a quality verdict. Durable tooling behavior belongs here; a generated report remains disposable evidence.
