# Audit Trail Manifest

**Date:** 2026-06-04  
**Per:** INVARIANT_THEORY.md §9.4 (Legacy Compatibility)  
**Reason:** These diagnostic artifacts were created during the debugging of build, verify, and runtime issues. They are preserved for historical audit but removed from the project root to restore workspace hygiene.

---

## Category A: Debug Python Scripts

All `run_*.py` scripts were one-time wrappers to invoke `dotnet build`, `dotnet run`, or `verify-dir` with various parameter combinations to isolate failures.

| File | Purpose |
|------|---------|
| `run_build.py` | Generic `dotnet build` wrapper |
| `run_build_builder.py` | Build `Vantuz.Builder.csproj` specifically |
| `run_build_builder2.py` | Build `Vantuz.Builder.csproj` (variant 2) |
| `run_build_builder3.py` | Build `Vantuz.Builder.csproj` (variant 3) |
| `run_build_builder4.py` | Build `Vantuz.Builder.csproj` (variant 4) |
| `run_builder_verify.py` | Run builder with verify (variant 1) |
| `run_builder_verify2.py` | Run builder with verify (variant 2) |
| `run_builder_verify3.py` | Run builder with verify (variant 3) |
| `run_builder_verify_release.py` | Run builder with verify in Release |
| `run_builder_noverify.py` | Run builder without verify |
| `run_dotnet_run_raw.py` | Raw `dotnet run` diagnostic |
| `run_dotnet_version.py` | Check dotnet CLI version |
| `run_rebuild.py` | `dotnet build --no-incremental` wrapper |
| `run_clean.py` | `dotnet clean` wrapper |
| `run_verify.py` | Generic verify wrapper (variant 1) |
| `run_verify2.py` | Generic verify wrapper (variant 2) |
| `run_headless.bat` | Batch script for headless launch |
| `fix_items.py` | Fix project item group references |
| `fix_proj.py` | Fix project file structure |
| `inspect_plugin.py` | Inspect plugin assemblies |
| `check_ts.py` | Check file timestamps |

## Category B: Diagnostic PowerShell Scripts

| File | Purpose |
|------|---------|
| `test_psroot.ps1` | Test PowerShell root execution context |
| `test_psroot2.ps1` | Test PowerShell root context (variant 2) |
| `test_scope.ps1` | Test PowerShell scope behavior |
| `mini.ps1` | Mini orchestrator (experimental) |
| `auto-fix-orchestrator2.ps1` | Duplicate/experimental orchestrator |

## Category C: Representative Logs

| File | Purpose |
|------|---------|
| `build_msbuild3.log` | Most complete MSBuild log from debugging |
| `temp_launcher_test.log` | Headless launcher test output |
| `temp_orchestrator5.log` | Final orchestrator run transcript |
| `verify-output.txt` | Full verify-dir output |
| `auto-fix-report.json` | Last auto-fix cycle report |
| `auto-fix-transcript.log` | Auto-fix transcript |

## Category D: Other Artifacts

| File | Purpose |
|------|---------|
| `boot.template.json.bak` | Backup of boot template before edits |

---

## Falsifier

If any future issue requires comparing against these debugging attempts, the archived files are available here. If they are ever needed in a future audit, their absence from the root would be a gap — mitigated by this manifest.
