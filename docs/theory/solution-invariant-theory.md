# Solution-Invariant Verification Theory

**Formalization:**
```
∀solution s:
    ∀invariant i ∈ ActiveInvariants:
        s ⊨ i ∨ DeviationProtocol(i) active
```

---

## Meta-Principle Axioms

### Axiom MP-1: Popperian Falsifiability

Every solution must expose a **falsifier** — a script, test, or deterministic check that can prove the solution violates an active invariant.

**Formal:**
```
∀solution s:
    ∃falsifier_f: Artifact → {Valid, Invalid}:
        falsifier_f(s) = Invalid ⟹ ∃i ∈ ActiveInvariants: s ⊭ i
```

**Consequence:** No solution may be justified by "it feels correct," "it works," or "it looks good." Only verifiable claims.

---

### Axiom MP-2: Occam's Constraint (Minimum Invariant Set)

No new invariant may be added unless an **existing invariant cannot cover the gap**.

**Formal:**
```
AddInvariant(i_new) valid ⟺
    ∀i_existing ∈ ActiveInvariants:
        i_existing does NOT cover i_new's violation class
```

**Consequence:** Invariant proliferation is forbidden. Each invariant must have a unique, non-overlapping violation class.

---

### Axiom MP-3: Leibniz Sufficient Reason

Every solution must cite the **exact invariant** it satisfies. No implicit justifications.

**Formal:**
```
∀solution s:
    justification(s) = {(i, evidence_i) | i ∈ ActiveInvariants, evidence_i proves s ⊨ i}
```

**Consequence:** A code change without an explicit invariant citation in the commit message or PR description is a **Solution-Invariant Violation** (AGENT-REC-022).

---

### Axiom MP-4: Courant Compromise Documentation

If a solution satisfies invariant A but weakens invariant B, the trade-off must be documented as a **DeviationProtocol** with deadline and justification.

**Formal:**
```
s ⊨ A ∧ s ⊭ B ⟹
    DeviationProtocol(B, deadline, justification) is active
    ∧ justification cites COMPOSITUM_SPEC §7.2
```

**Consequence:** No silent weakening of invariants. All compromises are explicit, time-bounded, and justified.

---

## Active Invariants Registry

| ID | Invariant | Origin | Falsifier | Integration |
|---|---|---|---|---|
| INV-001 | Falsifiability Coverage | INVARIANT_THEORY §1.2 | `check-falsifiability.ps1` | `verify-session-exit.ps1` |
| INV-002 | CQRS Separation | INVARIANT_THEORY §2.2 | `audit-compliance.ps1` | `verify-session-exit.ps1` |
| INV-003 | Empty Catch Prohibition | INVARIANT_THEORY §1.1 | `exhaustive-audit.ps1` Cat 3 | `exhaustive-audit.ps1` |
| INV-004 | Encoding Invariant | This theory | `verify-encoding.ps1` | `verify-session-exit.ps1` |
| INV-005 | User-Perceptual-Feedback | This theory | `exhaustive-audit.ps1` Cat 5 | `exhaustive-audit.ps1` |
| INV-006 | Deviation Inventory | COMPOSITUM_SPEC §7.2 | `exhaustive-audit.ps1` Cat 6 | `exhaustive-audit.ps1` |
| INV-007 | Plan Compliance | COMPOSITUM_SPEC §0.3 | `exhaustive-audit.ps1` Cat 7 | `exhaustive-audit.ps1` |
| INV-008 | Recidivism Self-Audit | INVARIANT_THEORY §1.2a | `exhaustive-audit.ps1` Cat 8 | `exhaustive-audit.ps1` |
| INV-009 | Build Pass | INVARIANT_THEORY §1.2 | `dotnet build` | `verify-session-exit.ps1` |
| INV-010 | Test Pass | INVARIANT_THEORY §1.2 | `dotnet test` | `verify-session-exit.ps1` |
| INV-011 | Solution-Invariant Gate | This theory | `invariant-gate.ps1` | `verify-session-exit.ps1` |

## Verification Gate

`scripts/invariant-gate.ps1` implements Axioms MP-1 through MP-4:
- Input: list of changed files (from git diff or manual)
- For each changed file, checks all applicable invariants
- Output: PASS/FAIL per invariant, with explicit citations

---

*Formal theory per INVARIANT_THEORY §1.2a (Reflexive Measurability) and §10.3 (Compositionality).*
