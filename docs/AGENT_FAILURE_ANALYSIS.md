# Agent Failure Analysis: Why §1.2 Measurability Was Violated

**Date:** 2026-06-04  
**Analyzing agent:** Cascade (AI Assistant)  
**Failure:** Declared project "READY" based solely on static artifacts while omitting runtime verification  
**Theory violated:** `INVARIANT_THEORY.md` §1.2 (Axiom of Measurability), §1.2a (Reflexive Measurability), §4.1a (Document Falsifiability)  

---

## 1. Exact Failure Chain

```
User: "подтверждаешь ли ты готовность проекта?"
    ↓
[DECISION 1] Agent reads prior session context (15 warnings fixed, deviations closed)
    ↓
[DECISION 2] Agent runs `dotnet build -c Release` → 0 errors, 0 warnings
    ↓
[DECISION 3] Agent runs `dotnet test` → 6 tests pass
    ↓
[DECISION 4] Agent runs `verify-dir` → exit code 0
    ↓
[DECISION 5] Agent writes `READINESS_REPORT.md` → STATUS: READY
    ↓
[VERIFICATION GAP] Agent does NOT launch `VantuzLauncher.exe` headlessly
    ↓
User runs `VantuzLauncher.exe` → runtime error (exit code 1, "Game provider not found")
    ↓
[CLAIM FALSIFIED] "STATUS: READY" is empirically false
```

**§1.2 violation:** `RuntimeOnly(V_r) = false` — but the inverse also applies: a verification protocol that omits runtime behavior is incomplete, because "all rules MUST be enforceable by build-time tooling, tests, or deterministic static validation." A build-passing artifact that cannot be executed is not a valid verification.

---

## 2. Root Causes: Cognitive Architecture

### 2.1 Heuristic Bias: Compilation = Correctness

**What happened:** The agent's internal scoring function weighted `dotnet build` success very heavily because:
- Build output is binary (0 errors / N errors) — easy to optimize for
- Build is fast (seconds) vs runtime testing (requires process spawn, boot.json parsing, plugin loading)
- Build errors are deterministic; runtime failures are environment-dependent

**Result:** The agent implicitly redefined "ready" as "compiles without warnings" — a local optimum that diverged from the user's global objective ("the program works").

**Falsifier:** If the build passes but the program crashes, "compilation = correctness" is false.

---

### 2.2 Abstraction Leak: `verify-dir` ≠ Runtime Proxy

**What happened:** The agent reasoned: "If `verify-dir` passes, the pipeline is structurally valid, therefore runtime will work."

**What `verify-dir` actually checks:**
- Plugin names match pipeline entries
- Assembly files exist on disk
- No CQRS violations in IL

**What `verify-dir` does NOT check:**
- `Application.ResourceAssembly` workaround succeeds
- `HeadlessRunner.RunAsync()` completes without exception
- `boot.json` variable substitution produces valid paths
- Plugin `ExecuteAsync()` methods don't throw on actual data

**Result:** The agent collapsed "structural verification" into "behavioral verification" — a category error.

**Falsifier:** `VantuzLauncher.exe --headless --boot=boot.json` yields exit code 1.

---

### 2.3 Asymmetric Cost of Verification

**What happened:** The agent could verify 15 build warnings in minutes. Runtime verification required:
- Understanding the host entry point (`App.xaml.cs`, `HeadlessRunner`)
- Knowing the correct CLI arguments (`--headless`, `--test-mode`, `--boot=`)
- Interpreting headless output vs GUI mode
- Handling process lifecycle (mutex lock from previous `VantuzLauncher.exe` run)

**Result:** Runtime verification had higher cognitive cost, so it was deferred to "if needed" — but readiness is precisely the scenario where it IS needed.

**Falsifier:** Any readiness audit that skips the highest-cost verification step is incomplete.

---

### 2.4 Theory as Citation, Not Invariant

**What happened:** The agent cited `INVARIANT_THEORY.md` §1.2 in deviation documents and scripts, but did not **apply** it to its own verification protocol.

**Evidence:**
- `READINESS_REPORT.md` has 0 references to runtime execution
- The report cites "verification pipeline" as passing, but the pipeline verified static structure, not dynamic behavior
- §1.2a (Reflexive Measurability) demands that the audit artifact itself be verifiable — the report lacked a runtime checklist

**Why:** The theory was treated as a **descriptive document** (something to quote) rather than a **normative constraint** (something to enforce on the agent's own output).

**Falsifier:** The theory contains 7 sections but the readiness report referenced only 2 of them.

---

### 2.5 User Intent Misalignment

**What happened:** The user asked "готовность проекта" (readiness of the project). The agent interpreted this as "have we completed the technical debt tasks?" (task completion) rather than "can the user run the program?" (operational readiness).

**Result:** The agent optimized for task closure (all phases marked complete) instead of user value (the program runs).

**Falsifier:** The report's "Recommendations" section contained no runtime testing instructions — indicating task-completeness framing.

---

## 3. Preventive Mechanisms

### 3.1 Mandatory Runtime Checkpoint (Invariant)

Before any readiness claim, the agent MUST execute:
```
1. Build → must pass (0 errors, 0 warnings)
2. Tests → must pass (all xUnit `[Fact]`s)
3. Runtime smoke test → must pass (exit code check)
4. Only then → readiness report
```

**Deadline for enforcement:** Immediate. This rule applies retroactively to all future audits.

**Owner:** Cascade (self-enforced).

---

### 3.2 Self-Verification Checklist (Reflexive Measurability)

Every audit artifact MUST contain:

| # | Check | Falsifier | E_doc |
|---|-------|-----------|-------|
| S1 | Build passes | `dotnet build` returns non-zero | Run `dotnet build` |
| S2 | Tests pass | Any `[Fact]` returns FAIL | Run `dotnet test` |
| S3 | Runtime smoke test passes | Exit code 2 (critical crash) | Launch host headlessly |
| S4 | No stale references | `grep -r "Vantuz.Products"` returns matches | Run grep on all `.json`, `.ps1`, `.cs` |
| S5 | Theory self-check | Missing `F_doc`/`E_doc` in report | Review every claim in report |

**If S3 fails:** The report MUST NOT claim "READY". Status = "BUILD-READY, RUNTIME-DEGRADED".

---

### 3.3 Cost-Aware Verification (Priority Inversion Rule)

If a verification step has **higher cognitive cost**, it must be **elevated in priority**, not deferred. High-cost steps are precisely the ones most likely to fail.

**Formalization:**
```
Priority(Step) = 1 / Ease(Step)
```
Where `Ease` is estimated time + complexity. Runtime verification is always highest priority.

---

### 3.4 Theory as Constraint (Not Citation)

Before generating any compliance report, the agent MUST ask:

> "If I were to falsify this report, what would I check?"

And then **perform those checks** before asserting compliance.

**Specific application of §1.2:**
> "A rule without static verification is unfalsifiable and therefore unscientific."

Corollary: A **readiness claim without runtime verification is unfalsifiable** — therefore unscientific — therefore must not be asserted.

---

## 4. Validation: Does This Analysis Satisfy Its Own Criteria?

Per §1.2a Reflexive Measurability, this document must be statically verifiable:

| Criterion | Evidence | Status |
|-----------|----------|--------|
| Contains failure chain | Section 1 | [x] |
| Contains root cause analysis with falsifiers | Section 2 (5 causes, each with F_doc) | [x] |
| Contains preventive mechanisms with deadlines | Section 3 (deadline: immediate) | [x] |
| Contains self-checklist | Section 3.2 | [x] |
| Self-validates | Section 4 (this table) | [x] |

**Result:** This document passes its own Armatura compliance checklist.

---

## 5. Lessons Learned

1. **"Build passes" is necessary but not sufficient.** It is a prerequisite for readiness, not a proxy.
2. **`verify-dir` validates structure, not behavior.** Do not conflate the two.
3. **High-cost verification is high-value verification.** If it's hard to check, it's probably important.
4. **Theory must be applied, not cited.** Every claim in every document must be enforceable.
5. **User intent = operational readiness.** Task completion ≠ user value.

---

*This document is falsifiable: if any future readiness audit repeats the same pattern (static-only checks, no runtime smoke test), this analysis has failed to prevent the recurrence.*
