# Compliance Audit Report: CI/Test/Orchestrator Modernization

**Audit Date:** 2026-06-02  
**Auditor:** Cascade Agent  
**Scope:** All files modified in CHECKPOINT 2 (CI/Test/Orchestrator Modernization)  
**Reference Documents:** INVARIANT_THEORY.md v1.3.0, COMPOSITUM_SPECIFICATION.md v3.2.0, COMPOSITUM.md v3.3.0, ARMATURA_DOCUMENT_PROTOCOL.md v1.3.0  

---

## 1. Executive Summary

**Overall Verdict:** **PARTIALLY COMPLIANT** with actionable violations requiring immediate attention.

| Category | Passed | Failed | Score |
|----------|--------|--------|-------|
| Determinism (§1.1) | 8 | 1 | 88% |
| Measurability (§1.2) | 7 | 2 | 78% |
| Nomadic Invariant (§3.2) | 6 | 3 | 67% |
| CQRS Separation (§2.2) | 9 | 0 | 100% |
| Component Scope (§2.3) | 8 | 1 | 88% |
| Document Protocol (§3-§4) | 5 | 5 | 50% |
| **TOTAL** | **43** | **12** | **78%** |

---

## 2. Violations Table

| # | File | Line | Invariant | Severity | Issue | Recommendation |
|---|------|------|-----------|----------|-------|--------------|
| 1 | `boot.headless.json` | 8 | §3.2 Nomadic | **HIGH** | `installDir` uses `${special:ApplicationData}` → absolute env-dependent path | Use `{{workspace}}/.vantuzlauncher/instances/...` |
| 2 | `App.xaml.cs` | 33 | §3.2 Nomadic | **HIGH** | `DetermineWorkspace()` uses `Environment.SpecialFolder.ApplicationData` without nomadic marker | Default to `$PSScriptRoot` or explicit `--nomadic` flag |
| 3 | `manifest.json` | 32,43,55,67 | §3 Version Integrity | **CRITICAL** | All document hashes are placeholders; `enforce_hash_match=true` would fail | Compute and set actual SHA256 hashes |
| 4 | `auto-fix-orchestrator.ps1` | 230-270 | §1.2 Measurability | **HIGH** | `Invoke-FixPhase` is placeholder — never applies actual fix; hash check always fails | Implement actual fix mechanism or document as `DEVIATION-004` |
| 5 | `auto-fix-orchestrator.ps1` | 54-63 | §1.1 Determinism | **MEDIUM** | `Get-CodeHash` depends on `Sort-Object FullName` order; non-deterministic across locales | Sort by canonical path or use tree hash |
| 6 | `App.xaml.cs` | 38,182 | §2.3 Component Scope | **LOW** | Two duplicate hash functions (`CalculateHash`, `CalculateStringMD5` — both SHA256) | Remove `CalculateStringMD5`; consolidate |
| 7 | `manifest.json` | 4 | §3 Timestamp | **MEDIUM** | `last_updated` is `15:45:00` but changes made at `22:52:00` | Update timestamp |
| 8 | `manifest.json` | 74-93 | §4 Deviation Tracking | **MEDIUM** | `DEVIATION-003` exists but not listed in `deviations` array | Add DEVIATION-003 entry |
| 9 | `auto-fix-orchestrator.ps1` | 414-420 | §1.2 Measurability | **LOW** | `Iterations` reports `0` instead of `1` in success path | Document as PS 5.1 quirk or fix hashtable increment |
| 10 | `test-and-run.ps1` | 51 | §2.3 Component Scope | **LOW** | `$projectPath` assigned but never used (dead code) | Remove unused variable |
| 11 | `VantuzLauncher.csproj` | 11 | §9.4 Legacy | **LOW** | `GenerateTargetFrameworkAttribute=false` workaround not in any deviation doc | Add to DEVIATION-002 or create DEVIATION-004 |
| 12 | `GameInstallerCommand.cs` | 34 | §3.2 Nomadic | **LOW** | `Path.GetFullPath()` may resolve relative to current dir if `installDir` not absolute | Ensure `installDir` is always absolute or relative to workspace |

---

## 3. Per-File Analysis

### 3.1 `boot.headless.json`

**Status:** Mostly Compliant  
**Violations:** #1 (Nomadic — `installDir` absolute path)  
**Strengths:** `dryRun: true`, deterministic `Auth.TestAuthCommand`, explicit pipeline phases.

### 3.2 `GameInstallerCommand.cs`

**Status:** Compliant  
**Violations:** #12 (Minor Nomadic concern)  
**Strengths:** `dryRun` prevents state mutation, proper `Interpolate()`, CQRS-compliant.

### 3.3 `HeadlessRunner.cs`

**Status:** Compliant  
**Violations:** None  
**Strengths:** Immutable `TestResult`, `WorkspacePath` override, measurable JSON output.

### 3.4 `App.xaml.cs`

**Status:** Partially Compliant  
**Violations:** #2 (Nomadic — `ApplicationData` fallback), #6 (Duplicate hash functions)  
**Strengths:** `--workspace` and `--boot` overrides, deterministic headless mode.

### 3.5 `test-and-run.ps1`

**Status:** Compliant  
**Violations:** #10 (Dead code — `$projectPath`)  
**Strengths:** Nomadic paths, explicit arguments, proper exit codes.

### 3.6 `auto-fix-orchestrator.ps1`

**Status:** Partially Compliant  
**Violations:** #4 (Placeholder fix), #5 (Non-deterministic hash), #9 (Iterations=0 bug)  
**Strengths:** Unified report, retry guard, termination guarantee, `RunId` on all paths.

### 3.7 `VantuzLauncher.csproj`

**Status:** Compliant  
**Violations:** #11 (SDK workaround not documented)  
**Strengths:** Pre/post-build verification targets, DEVIATION-002 guards, clear error messages.

### 3.8 `.github/workflows/build-and-test.yml`

**Status:** Compliant  
**Violations:** None  
**Strengths:** Caching, artifact upload, status assertion.

### 3.9 `VantuzLauncher.sln`

**Status:** Compliant  
**Violations:** None

---

## 4. Document Cascade Analysis

### 4.1 `manifest.json` Integrity — **NON-COMPLIANT**

- **CRITICAL:** All four document hashes are placeholders (`sha256_updated_after_v1.3_changes`, `sha256_placeholder_update_after_edit`). With `enforce_hash_match: true`, any validator would fail.
- **MEDIUM:** `last_updated` (`2026-06-02T15:45:00+05:00`) is stale — actual changes made at ~22:52.
- **MEDIUM:** `DEVIATION-003.md` exists on disk but is absent from the `deviations` array.
- **MEDIUM:** `updated_by` reflects §12-§14 addition, not this CI modernization session.

### 4.2 Deviation Protocol Status

| Deviation | Document | Manifest | Consistent |
|-----------|----------|----------|------------|
| DEVIATION-001 | Active | active | ✅ Yes |
| DEVIATION-002 | Active (Phases 1-2 complete) | partially_resolved | ⚠️ Partially |
| DEVIATION-003 | Active | **Missing** | ❌ No |

### 4.3 Required Document Updates (per ARMATURA_DOCUMENT_PROTOCOL §4)

| Document | Action Required | Reason |
|----------|----------------|--------|
| `manifest.json` | Update hashes, timestamp, add DEVIATION-003 | §3.1, §3.2 |
| `COMPOSITUM.md` | No update required | Changes are implementation-level (Level 4) |
| `COMPOSITUM_SPECIFICATION.md` | Optional: document `dryRun` as test pattern | Changes instantiate existing spec, don't modify it |
| `INVARIANT_THEORY.md` | No update required | Universal invariants unchanged |
| `ARMATURA_DOCUMENT_PROTOCOL.md` | No update required | Protocol rules unchanged |

---

## 5. Compliance Score

**Overall: 43/55 checks passed = 78.2%**

| Severity | Count | Action |
|----------|-------|--------|
| CRITICAL | 1 | Must fix before next commit |
| HIGH | 3 | Must fix within 24 hours |
| MEDIUM | 4 | Must fix within 48 hours |
| LOW | 4 | Should fix within 1 week |

---

## 6. Recommendations

### Immediate (before next commit)

1. **Fix `manifest.json` hashes (#3):** Compute actual SHA256 for all four documents and update the `hash` fields. This is a CRITICAL violation of `enforce_hash_match: true`.

### Within 24 hours (HIGH priority)

2. **Fix Nomadic path in `boot.headless.json` (#1):** Change `installDir` to use a relative path (e.g., `"{{workspace}}/.vantuzlauncher/instances/1.20.1-forge"`) and ensure `GameInstallerCommand` resolves it relative to the workspace.

3. **Fix Nomadic default in `App.xaml.cs` (#2):** When `--workspace` is not provided and `.portable` is absent, default to `$PSScriptRoot` (for headless) or document the deviation explicitly.

4. **Address placeholder `Invoke-FixPhase` (#4):** Either implement an actual fix mechanism (e.g., regex-based fixes for known patterns) or register this as `DEVIATION-004: Auto-Fix Placeholder` with a deadline.

### Within 48 hours (MEDIUM priority)

5. **Update `manifest.json` metadata (#7, #8):** Update `last_updated`, add DEVIATION-003, set `updated_by` to reflect this session.

6. **Fix `Get-CodeHash` determinism (#5):** Sort files by a canonical property (e.g., relative path from `$scriptDir`) instead of `FullName` to ensure cross-platform determinism.

7. **Update DEVIATION-002 status:** Mark Phase 3 as pending and document the `GenerateTargetFrameworkAttribute=false` workaround.

### Within 1 week (LOW priority)

8. **Remove duplicate hash function in `App.xaml.cs` (#6):** Delete `CalculateStringMD5` and consolidate all hashing to `CalculateHash`.

9. **Remove dead code in `test-and-run.ps1` (#10):** Remove unused `$projectPath` variable.

10. **Document SDK workaround (#11):** Add `GenerateTargetFrameworkAttribute=false` to DEVIATION-002 resolution plan or create DEVIATION-004.

11. **Investigate `Iterations=0` (#9):** Either fix the PowerShell hashtable increment issue or document it as a known cosmetic issue.

---

## 7. Conclusion

The CI/Test/Orchestrator Modernization session successfully implemented all five planned phases and achieved functional correctness (build passes, tests pass, report generates). However, **document integrity (`manifest.json` placeholder hashes) and nomadic path compliance** represent significant gaps against the theoretical framework.

The **CRITICAL** `manifest.json` hash issue must be resolved before any further commits, as it directly violates `enforce_hash_match: true` in the manifest's own validation rules. The **HIGH** priority nomadic path violations in `boot.headless.json` and `App.xaml.cs` undermine the deterministic, environment-independent guarantees of the Armatura architecture.

Once these issues are addressed, the codebase will achieve **>90% compliance** across all invariants.

---

*Per ARMATURA_DOCUMENT_PROTOCOL.md §11.3 (Audit Requirements) and COMPOSITUM.md §4 (Deviation Protocol).*
