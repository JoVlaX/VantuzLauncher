# Theory Compliance Audit

**Artifact audited:** `retrospective-readiness-audit-622aab.md`  
**Against:** `INVARIANT_THEORY.md` (v1.1, 2026-05-30)  
**Auditor:** Cascade  
**Date:** 2026-06-04

---

## Executive Summary

**Result:** The retrospective plan **FAILS** Armatura compliance on 6 binding clauses. It cannot be approved in its current form.

| Violation | Theory Clause | Severity |
|-----------|-------------|----------|
| V1 — Missing ISO8601 deadlines | §9.4, §9.4a | **BLOCKER** |
| V2 — Claims without falsifier sets | §4.1a | **BLOCKER** |
| V3 — Reflexive measurability gap | §1.2a | **BLOCKER** |
| V4 — "Optional" = non-deterministic verification | §4.2 | **BLOCKER** |
| V5 — Manual checklist instead of automation | §10.3 | **MAJOR** |
| V6 — Exemption without owner/justification | §9.4 | **MINOR** |

---

## Detailed Findings

### V1: Missing ISO8601 Deadlines (§9.4, §9.4a)

**Theory text:**
> `ValidAction(a) ⟺ ∃Deadline(d): d ∈ ISO8601 ∧ d > Now()`  
> §9.4a: "Any time-bounded action — whether exemption (negative) or improvement (positive) — MUST have an ISO8601 deadline."

**Audit evidence:**
```
Phase 1: Reproduce & Document        — no deadline
Phase 2: Close the Audit Gap         — no deadline
Phase 3: Fix the Runtime Issue       — no deadline
Phase 4: Sign-Off                    — no deadline
```

**Consequence:** The plan is an unfalsifiable wishlist. Per §9.4 proof of consistency: an action with no deadline is not falsifiable — we can never verify whether it was completed on time.

**Fix:** Append `Deadline: <ISO8601>` to each phase and every sub-step.

---

### V2: Claims Without Falsifier Sets (§4.1a)

**Theory text:**
> `ValidClaim(c) ⟺ |F_doc(c)| > 0 ∧ |E_doc(c)| > 0`  
> "Every claim in an Armatura-compliant document must have concrete falsifier set `F_doc` and empirical test `E_doc`."

**Audit evidence:**

| Claim in Plan | F_doc (what would falsify?) | E_doc (how to detect?) | Status |
|---------------|------------------------------|------------------------|--------|
| "The previous readiness audit declared the project READY based solely on compile-time artifacts" | Not provided | Not provided | **INVALID** |
| "The audit focused on static correctness rather than dynamic behavior" | Not provided | Not provided | **INVALID** |
| "verify-dir does not execute the pipeline" | Not provided | Not provided | **INVALID** |
| "auto-fix-orchestrator runs in dry-run/hash mode — it never launches the actual host" | Not provided | Not provided | **INVALID** |

**Consequence:** These are agent-state claims about past behavior. Per §4.1a: "Claims depending on the generator's internal state... MUST be marked [HYPOTHESIS] and excluded from `ValidClaim` unless accompanied by an observable proxy."

**Fix:** For each claim, either:
1. Provide `F_doc` + `E_doc` (e.g., code inspection command, grep pattern, test output), OR
2. Mark as `[HYPOTHESIS]` and exclude from compliance assertions.

---

### V3: Reflexive Measurability Violation (§1.2a)

**Theory text:**
> "Any document, plan, or artifact asserting compliance with Armatura MUST itself be statically verifiable against the same criteria."  
> `AssertsCompliance(a, Armatura) → ∃V_a: Artifact → {Valid, Invalid}`

**Audit evidence:**
The plan asserts it will "implement a more rigorous verification protocol that includes mandatory runtime, integration, and GUI-mode testing." However, the plan contains **no verifiable checklist for itself**. A third party cannot statically verify whether the plan satisfies Armatura without re-deriving the analysis.

**Consequence:** The plan violates the very axiom it claims to uphold. This is a bootstrap failure.

**Fix:** Append a "Self-Verification Checklist" section with pass/fail criteria. Example:

```
Self-Check 1: Plan contains ≥1 ISO8601 deadline per phase → [ ] PASS / [ ] FAIL
Self-Check 2: Every claim has F_doc and E_doc → [ ] PASS / [ ] FAIL
Self-Check 3: No "optional" verification steps → [ ] PASS / [ ] FAIL
Self-Check 4: Manual steps are marked [MANUAL_TEST_REQUIRED] with justification → [ ] PASS / [ ] FAIL
```

---

### V4: "Optional" Smoke Test = Non-Deterministic Verification (§4.2)

**Theory text:**
> "MUST rules: Maximum falsifiability (absolute compliance checkable)"  
> "Degenerate Case: A rule like 'code should be good' is unfalsifiable—no concrete F_r exists."

**Audit evidence:**
> Phase 2.6: "Update auto-fix-orchestrator.ps1 — add an **optional** runtime smoke-test step"

**Consequence:** An optional step is a degenerate rule. The system can pass CI while skipping the smoke test, and no falsifier can detect the omission at build time. This directly contradicts §1.2: `RuntimeOnly(V_r) = false`.

**Fix:** Change "optional" to "mandatory". Specify deterministic pass/fail criteria:
```
MANDATORY: dotnet run --project VantuzLauncher -- --headless --boot=boot.json
E_doc: Process exit code MUST be 0
F_doc: exit code ≠ 0 OR process crashes with unhandled exception
```

---

### V5: Manual Checklist Instead of Automated Verification (§10.3)

**Theory text:**
> "Unity of Principles #4: **Verifiability over Trust** (static→runtime, **automated→manual**)"

**Audit evidence:**
> Phase 2.5: "Update READINESS_REPORT.md — add a Runtime Verification section with **mandatory checklist items**"

**Consequence:** A manual checklist relies on human judgment — the exact failure mode that caused the original audit gap. The theory explicitly prefers automation because humans are unreliable falsifiers.

**Fix:** Replace manual checklist items with automated tests:
- `[Fact] public void HeadlessSmokeTest_ExitsZero()` — launches process, asserts exit code
- `[Fact] public void BootJson_LoadsWithoutException()` — parses boot.json, asserts no null refs
- If GUI test cannot be automated, mark `[MANUAL_TEST_REQUIRED]` with causal justification.

---

### V6: Exemption Without Owner/Justification (§9.4)

**Theory text:**
> `ValidExemption(e) ⟺ ∃Marker(m) ∧ ∃Deadline(d) ∧ ∃Justification(j) ∧ ∃Owner(o)`

**Audit evidence:**
> Phase 3.10: "File a DEVIATION or TODO if the fix requires architectural work"

This is an implicit exemption (deferring work). It lacks:
- `Marker`: Not specified (TODO? FIXME? DEVIATION?)
- `Deadline`: Not provided
- `Justification`: No causal link to architectural constraint
- `Owner`: Not assigned

**Fix:** If architectural work is deferred, file a full DEVIATION-00x with all four fields.

---

## Amendments Required

| # | Amendment | Clause |
|---|-----------|--------|
| A1 | Add `Deadline: <ISO8601>` to each phase and sub-step | §9.4a |
| A2 | Append `F_doc` and `E_doc` to every claim, or mark `[HYPOTHESIS]` | §4.1a |
| A3 | Add a self-checklist with pass/fail criteria | §1.2a |
| A4 | Change "optional smoke-test" → "mandatory" with exit-code assertion | §4.2 |
| A5 | Replace manual checklist with `[Fact]`-based automated tests | §10.3 |
| A6 | If work is deferred, file full DEVIATION with owner/deadline/justification | §9.4 |

---

## Conclusion

The retrospective plan is **theoretically non-compliant** in its current form. It makes strong claims about Armatura compliance while violating the reflexive measurability, falsifiability, and deadline requirements of the theory itself.

**Recommendation:** Reject the plan. Require amendments A1–A6 before re-submission. Once amended, the plan must pass its own self-checklist (A3) before any implementation begins.

**Sign-off:**

| Reviewer | Status |
|----------|--------|
| Theory Compliance | **REJECTED — amendments required** |
