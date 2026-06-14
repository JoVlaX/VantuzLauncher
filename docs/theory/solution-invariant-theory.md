# Solution-Invariant Verification Theory

**Formalization:**
```
в€Ђsolution s:
    в€Ђinvariant i в€€ ActiveInvariants:
        s вЉЁ i в€Ё DeviationProtocol(i) active
```

---

## Meta-Principle Axioms

### Axiom MP-1: Popperian Falsifiability

Every solution must expose a **falsifier** вЂ” a script, test, or deterministic check that can prove the solution violates an active invariant.

**Formal:**
```
в€Ђsolution s:
    в€ѓfalsifier_f: Artifact в†’ {Valid, Invalid}:
        falsifier_f(s) = Invalid вџ№ в€ѓi в€€ ActiveInvariants: s вЉ­ i
```

**Consequence:** No solution may be justified by "it feels correct," "it works," or "it looks good." Only verifiable claims.

---

### Axiom MP-2: Occam's Constraint (Minimum Invariant Set)

No new invariant may be added unless an **existing invariant cannot cover the gap**.

**Formal:**
```
AddInvariant(i_new) valid вџє
    в€Ђi_existing в€€ ActiveInvariants:
        i_existing does NOT cover i_new's violation class
```

**Consequence:** Invariant proliferation is forbidden. Each invariant must have a unique, non-overlapping violation class.

---

### Axiom MP-3: Leibniz Sufficient Reason

Every solution must cite the **exact invariant** it satisfies. No implicit justifications.

**Formal:**
```
в€Ђsolution s:
    justification(s) = {(i, evidence_i) | i в€€ ActiveInvariants, evidence_i proves s вЉЁ i}
```

**Consequence:** A code change without an explicit invariant citation in the commit message or PR description is a **Solution-Invariant Violation** (AGENT-REC-022).

---

### Axiom MP-4: Courant Compromise Documentation

If a solution satisfies invariant A but weakens invariant B, the trade-off must be documented as a **DeviationProtocol** with deadline and justification.

**Formal:**
```
s вЉЁ A в€§ s вЉ­ B вџ№
    DeviationProtocol(B, deadline, justification) is active
    в€§ justification cites COMPOSITUM_SPEC В§7.2
```

**Consequence:** No silent weakening of invariants. All compromises are explicit, time-bounded, and justified.

---

## Active Invariants Registry

| ID | Invariant | Origin | Falsifier | Integration |
|---|---|---|---|---|
| INV-001 | Falsifiability Coverage | INVARIANT_THEORY В§1.2 | `check-falsifiability.ps1` | `verify-session-exit.ps1` |
| INV-002 | CQRS Separation | INVARIANT_THEORY В§2.2 | `audit-compliance.ps1` | `verify-session-exit.ps1` |
| INV-003 | Empty Catch Prohibition | INVARIANT_THEORY В§1.1 | `exhaustive-audit.ps1` Cat 3 | `exhaustive-audit.ps1` |
| INV-004 | Encoding Invariant | This theory | `verify-encoding.ps1` | `verify-session-exit.ps1` |
| INV-005 | User-Perceptual-Feedback | This theory | `exhaustive-audit.ps1` Cat 5 | `exhaustive-audit.ps1` |
| INV-006 | Deviation Inventory | COMPOSITUM_SPEC В§7.2 | `exhaustive-audit.ps1` Cat 6 | `exhaustive-audit.ps1` |
| INV-007 | Plan Compliance | COMPOSITUM_SPEC В§0.3 | `exhaustive-audit.ps1` Cat 7 | `exhaustive-audit.ps1` |
| INV-008 | Recidivism Self-Audit | INVARIANT_THEORY В§1.2a | `exhaustive-audit.ps1` Cat 8 | `exhaustive-audit.ps1` |
| INV-009 | Build Pass | INVARIANT_THEORY В§1.2 | `dotnet build` | `verify-session-exit.ps1` |
| INV-010 | Test Pass | INVARIANT_THEORY В§1.2 | `dotnet test` | `verify-session-exit.ps1` |
| INV-011 | Solution-Invariant Gate | This theory | invariant-gate.ps1 | erify-session-exit.ps1 |
| INV-012 | Commit Protocol | This theory | erify-commit.ps1 | erify-session-exit.ps1 |

## INV-005b: User-Perceptual-Feedback Sub-Invariants

**Statement:** Every user-facing executable MUST satisfy the following measurable UX constraints.

**Sub-Invariant 5b.1 вЂ” Startup Feedback:**
Visible or audible indication MUST appear within 300ms of `Main()` entry.
- `F_doc`: `{Stopwatch shows >300ms from Main() entry to first visible pixel or title change}`
- `E_doc`: `{Stopwatch measurement in CI; gate fails if >300ms without feedback}`

**Sub-Invariant 5b.2 вЂ” Console Encoding:**
All textual output to console MUST be UTF-8 encoded.
- `F_doc`: `{Console.OutputEncoding != Encoding.UTF8}`
- `E_doc`: `{Runtime assertion: [Console]::OutputEncoding -eq UTF8}`

**Sub-Invariant 5b.3 вЂ” Process Cancellation:**
Every long-running fire-and-forget command MUST register an `IRunningProcessHandle` in `CommandContext` and respond to `CancellationToken`.
- `F_doc`: `{CancellationToken.IsCancellationRequested == true but child process survives >5s}`
- `E_doc`: `{Process existence assertion post-cancellation in test}`

**Sub-Invariant 5b.4 вЂ” Input Visibility:**
Text input fields MUST maintain >= 4.5:1 contrast ratio between text and background in all states (normal, focused, disabled).
- `F_doc`: `{Background color on focus becomes white or transparent (theme-dependent)}`
- `E_doc`: `{Color contrast calculation or visual diff test}`

**Sub-Invariant 5b.5 вЂ” Quantized Numeric Input:**
Sliders/spinners for hardware resources (RAM, disk) MUST snap to standard engineering multiples (1024 for bytes).
- `F_doc`: `{Value % 1024 != 0 after user interaction}`
- `E_doc`: `{Unit test fuzzing all interaction paths}`

**Sub-Invariant 5b.6 вЂ” Application Icon:**
The main window and taskbar entry MUST display a non-default icon.
- `F_doc`: `{Window.Icon == null AND hIcon == Zero}`
- `E_doc`: `{Headless test asserts Icon != null}`

## Verification Gate

`scripts/invariant-gate.ps1` implements Axioms MP-1 through MP-4:
- Input: list of changed files (from git diff or manual)
- For each changed file, checks all applicable invariants
- Output: PASS/FAIL per invariant, with explicit citations

---

*Formal theory per INVARIANT_THEORY В§1.2a (Reflexive Measurability) and В§10.3 (Compositionality).*



**Sub-Invariant 5b.7 - Clean Working Tree:**
After exhaustive-audit PASS, working tree MUST be clean (committed).
- F_doc: {audit PASS but git status shows uncommitted changes}
- E_doc: {exhaustive-audit.ps1 runs 'git status --short' and fails if non-empty}

**Sub-Invariant 5b.8 - Invariant-Derived Message:**
Every commit MUST reference the invariant IDs satisfied by the change.
- F_doc: {git log --format=%B -n 1 | Select-String 'INV-' returns 0 matches}
- E_doc: {verify-commit.ps1 asserts INV-XXX in commit message body}

