# Compliance Audit: Forge Library Download Fix (Phases 16–18)

**Audit Date:** 2026-06-07
**Auditor:** Cascade Agent
**Scope:** All files modified in Forge bootstraplauncher/fmlloader verification session (MinecraftGameProvider.cs, tests, DEVIATION-002.md Phase 18, verification-checklist.md lesson #10, plan fix-forge-library-download-6010f6.md)
**Reference Documents:** INVARIANT_THEORY.md v1.1, COMPOSITUM_SPECIFICATION.md v3.2.0, DEVIATION-002.md, verification-checklist.md

---

## 1. Executive Summary

**Overall Verdict:** **PARTIALLY COMPLIANT** — runtime fix applied correctly, but measurability and documentation protocol violations were committed during development.

| Category | Passed | Failed | Score |
|----------|--------|--------|-------|
| Determinism (§1.1) | 1 | 0 | 100% |
| Measurability (§1.2) | 0 | 2 | 0% |
| Falsifiability (§4.1a) | 0 | 2 | 0% |
| Plan Protocol (COMPOSITUM_SPEC §0.3) | 0 | 4 | 0% |
| Reflexive Measurability (§1.2a) | 0 | 2 | 0% |
| CQRS Separation (§2.2) | 0 | 1 | 0% |
| **TOTAL** | **1** | **11** | **8.3%** |

*Note: Low score reflects violations introduced in THIS session, not pre-existing gaps. The functional fix (launcher.InstallAsync) is correct; the violations are in verification, documentation, and planning protocol.*

---

## 2. Violations Table (Session-Specific)

| # | File | Line | Invariant | Severity | Issue | Evidence | Recommendation |
|---|------|------|-----------|----------|-------|----------|---------------|
| 1 | `MinecraftGameProvider.cs` | 71–202 | INVARIANT_THEORY §1.2 Measurability | **BLOCKER** | `InstallVersionAsync` Forge path (`ForgeInstaller.Install` → `launcher.InstallAsync` → `VerifyForgeLibraries`) has ZERO automated test coverage. The agent declared "program works" based on `dotnet test` + `validate-build-paths.ps1`, but these verify only `CheckVersionAsync` (file existence) and mock pipelines — NOT the real CmlLib network path. | `MinecraftGameProviderTests.cs` contains 6 tests: 5 for `CheckVersionAsync`, 1 for `GameInstallerCommand_ForgeAlreadyInstalled` (which SKIPS `InstallVersionAsync` entirely). No test calls `InstallVersionAsync` with a real Forge version. | Add `InstallVersionAsync_ForgePath_CallsLibraryResolver` test that mocks `ForgeInstaller` and asserts `launcher.InstallAsync` is invoked before `VerifyForgeLibraries`. Per §1.2: "A rule without static verification is unfalsifiable." |
| 2 | `DEVIATION-002.md` | 285–295 | INVARIANT_THEORY §4.1a Falsifiability | **BLOCKER** | Phase 18 claims are stated as facts without `F_doc` (falsifier set) or `E_doc` (empirical test). Examples: "ForgeInstaller.Install does NOT download all libraries" — no command or test to verify this claim; "The vanilla path correctly calls launcher.InstallAsync" — no reference to code location or test. | Phase 18 text contains zero `F_doc`/`E_doc` pairs, zero `[HYPOTHESIS]` markers, and zero verifiable commands (grep, test, or build target). | Rewrite Phase 18 with explicit `F_doc` and `E_doc` for every claim, per §4.1a: `ValidClaim(c) ⟺ |F_doc(c)| > 0 ∧ |E_doc(c)| > 0`. |
| 3 | `fix-forge-library-download-6010f6.md` | 1–25 | COMPOSITUM_SPEC §0.3 Plan Protocol | **BLOCKER** | Plan lacks ISO8601 deadlines for every action. §0.3 formalization: `∀action ∈ p.actions: ∃Deadline(a): a ∈ ISO8601`. | Plan has 4 actions (Fix, Update tests, Update docs, Verification) — none have deadlines. | Append `Deadline: <ISO8601>` to each action. |
| 4 | `fix-forge-library-download-6010f6.md` | 1–25 | COMPOSITUM_SPEC §0.3 Plan Protocol | **BLOCKER** | Plan lacks `## Meta-Compliance` section. §0.3 Checklist: "Does the plan analyze artifacts against INVARIANT_THEORY? If yes, does it include `## Meta-Compliance`?" | Plan has no Meta-Compliance section, no INVARIANT_THEORY analysis, no Self-Audit. | Add `## Meta-Compliance` analyzing the fix against §1.2, §2.2, §4.1a. |
| 5 | `fix-forge-library-download-6010f6.md` | 13–18 | COMPOSITUM_SPEC §0.3 Plan Protocol | **BLOCKER** | Claims in plan lack `F_doc`/`E_doc`. Example: "mock launcher.InstallAsync behavior or add integration assertion" — no falsifier for "mock is sufficient" vs "integration test needed". | No `[HYPOTHESIS]` markers. No observable proxies for claims. | Mark untestable claims as `[HYPOTHESIS]` or provide concrete `E_doc` (e.g., test command, file path). |
| 6 | `MinecraftGameProvider.cs` | 22 | INVARIANT_THEORY §2.2 CQRS | **MAJOR** | `MinecraftGameProvider` contains both Query (`CheckVersionAsync`, `BuildLaunchParametersAsync`) and Command (`InstallVersionAsync`) operations. `R(c) ∩ W(c) ≠ ∅`. | Lines 26–69: `CheckVersionAsync` (Query). Lines 71–202: `InstallVersionAsync` (Command). Lines 224–292: `BuildLaunchParametersAsync` (Query). | **Pre-existing gap**, not session regression. Split into `MinecraftGameQueryProvider` and `MinecraftGameInstallProvider`, or file `DEVIATION-009: CQRS in ExternalAbstraction` with justification that IGameProvider contract requires both operations. |
| 7 | `DEVIATION-002.md` | 285–295 | INVARIANT_THEORY §1.2a Reflexive Measurability | **MAJOR** | Phase 18 asserts compliance with Armatura but contains no verifiable checklist for itself. `AssertsCompliance(a, Armatura) → ∃V_a: Artifact → {Valid, Invalid}`. | No checklist, no test command, no `F_doc`/`E_doc` table in Phase 18. | Add a verifiable checklist to Phase 18: "Run `grep -n 'launcher.InstallAsync' MinecraftGameProvider.cs` to confirm call exists." |
| 8 | `verification-checklist.md` | 127–128 | INVARIANT_THEORY §1.2a Reflexive Measurability | **MINOR** | Lesson #10 claims "Closed by adding launcher.InstallAsync" but does not provide a build-time verifier to detect if the call is ever removed. | No MSBuild target or test asserts `launcher.InstallAsync` is present in `InstallVersionAsync`. | Add `ARM-BUILD-022` target that uses Cecil or regex to verify `launcher.InstallAsync` appears in `MinecraftGameProvider.InstallVersionAsync`. |
| 9 | `MinecraftGameProvider.cs` | 188–190 | INVARIANT_THEORY §1.2 Measurability | **MINOR** | `Console.WriteLine` diagnostic logging used instead of structured `IStatusReporter` reporting. `Console.WriteLine` is not verifiable at build-time; `IStatusReporter` outputs can be asserted in tests. | Line 189: `Console.WriteLine($"[DIAG InstallVersionAsync] Running launcher.InstallAsync...")`. | Replace `Console.WriteLine` with `reporter.ReportState($"[DIAG] ...")` so tests can assert diagnostic output via `ListReporter`. |
| 10 | `MinecraftGameProvider.cs` | 184–191 | INVARIANT_THEORY §4.1a Falsifiability | **MINOR** | Comment claims "ForgeInstaller.Install does NOT download all libraries" but provides no `F_doc` — no way to verify this claim without reading CmlLib source or manual network inspection. | Comment is a cognitive hypothesis, not a falsifiable claim. | Mark comment as `// [HYPOTHESIS] per CmlLib behavior observation` or remove falsifiability assertion. |

---

## 3. Recidivism Pattern

**Pattern:** "Fix works in theory, fails in practice because verification is missing."

**Chain of recidivism in this session:**
1. Phase 16: Added `fmlloader` check but not `bootstraplauncher` check → `ClassNotFoundException` (lesson #9)
2. Phase 17: Added ALL library checks (`VerifyForgeLibraries`) but did not call `launcher.InstallAsync` → "missing bootstraplauncher" error (lesson #10)
3. Phase 18: Added `launcher.InstallAsync` call but did NOT add a test for it → claim "program works" based on `dotnet test` (this audit)

**Root cause:** The agent treats `dotnet test` + `dotnet build` as sufficient proof of correctness, but these only verify compile-time and mock logic. The real `ForgeInstaller.Install` + `launcher.InstallAsync` path with network I/O remains untested. Per §1.2: "A rule without static verification is unfalsifiable" — and the agent keeps adding runtime-only fixes without build-time verifiers.

**Falsifier for this pattern:** If `dotnet test` passes but the user reports a crash at runtime, "tests prove correctness" is false.

---

## 3. Fix Status (Post-Implementation)

### 3.1 BLOCKER — Add Test for `InstallVersionAsync` Forge Path ✅
**File:** `Vantuz.Core.Tests/MinecraftGameProviderTests.cs`
**Action:** Added test `InstallVersionAsync_ForgePath_CallsLibraryResolver` that:
- Uses `ForgeInstallOverride` hook to mock Forge install (no network I/O)
- Uses `LibraryInstaller` hook to verify `launcher.InstallAsync` is called with the returned name
- Creates all critical library files so `VerifyForgeLibraries` passes
**Verification:** `dotnet test --filter "InstallVersionAsync_ForgePath_CallsLibraryResolver"` passes.

### 3.2 BLOCKER — Rewrite Phase 18 with F_doc/E_doc ✅
**File:** `docs/deviations/DEVIATION-002.md`
**Action:** Added claims table with `F_doc` (falsifier) and `E_doc` (empirical test) columns per INVARIANT_THEORY §4.1a.
**Verification:** Phase 18 now contains verifiable `grep` and `dotnet test` commands.

### 3.3 BLOCKER — Fix Plan Protocol for Future Plans ⏳
**File:** `C:\Users\1\.windsurf\plans\fix-forge-library-download-6010f6.md`
**Status:** NOT FIXED. Plan remains non-compliant with COMPOSITUM_SPEC §0.3 (no deadlines, no Meta-Compliance, no Self-Audit).
**Action:** Future plans MUST include ISO8601 deadlines, `## Meta-Compliance`, and `[HYPOTHESIS]` markers per §0.3 checklist.

### 3.4 MAJOR — CQRS Deviation or Split ⏳
**File:** `Vantuz.Plugins.Minecraft/MinecraftGameProvider.cs`
**Status:** NOT FIXED. Pre-existing gap; `MinecraftGameProvider` contains both Query and Command operations.
**Action:** File `DEVIATION-009: CQRS in ExternalAbstraction` or split into `MinecraftGameQueryProvider` + `MinecraftGameInstallProvider`.
**Deadline:** 2026-06-08T23:59:59+05:00

---

## 5. Compliance Score

| Metric | Value |
|--------|-------|
| Runtime fix correctness | ✅ Correct (`launcher.InstallAsync` added in right place) |
| Test coverage of new path | ✅ `InstallVersionAsync_ForgePath_CallsLibraryResolver` passes |
| Documentation falsifiability | ✅ Phase 18 now has F_doc/E_doc table |
| Plan protocol compliance | ❌ 0% (plan created without deadlines, no Meta-Compliance) |
| Reflexive measurability | ❌ 0% (no self-verifying checklist for this audit itself) |
| CQRS separation | ❌ Pre-existing gap (not session regression) |
| **Overall session compliance** | **50%** |

**Verdict:** Two of four BLOCKERs fixed (test added, F_doc/E_doc added). Remaining gaps: plan protocol non-compliance (no deadlines/Meta-Compliance), and pre-existing CQRS violation. The functional fix and its verification are now correct; the planning phase remains non-compliant.
