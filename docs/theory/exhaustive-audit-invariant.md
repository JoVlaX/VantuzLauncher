# Exhaustive-Analysis Invariant

**Formalization:**
```
∀analysis_request:
    analysis_report.completeness = exhaustive
    ∧ completeness_verifiable_by(second_pass)
```

---

## Axiomatic Foundation

### Axiom 1: Popperian Completeness (Falsifiability)

The claim "all violations found" must have a **falsifier** — a method that, if it finds even one new violation, proves the claim false.

**Formal:**
```
F_r = {analysis_report claiming completeness | second_pass finds >0 new violations}
E_r = {exhaustive-audit.ps1 run twice; violation_count_1 == violation_count_2}
```

If `violation_count_2 > violation_count_1`, the first report is falsified.

### Axiom 2: Occam's Enumeration (Minimum Description Length)

The set of violation categories must be the **minimum complete set** derivable from COMPOSITUM_SPEC and INVARIANT_THEORY.

**Formal:**
```
Let C = {all verifiable clauses in SPEC ∪ THEORY}
Let V = {violation categories in exhaustive-audit.ps1}
Invariant: V = {c ∈ C | c produces a deterministic V_r(c)}
```

No category may be added without a SPEC/THEORY citation. No category from SPEC/THEORY may be omitted.

### Axiom 3: Leibniz Sufficient Reason

Every check in the exhaustive audit must cite the **sufficient reason** — the exact paragraph in SPEC or THEORY that mandates it.

**Current mapping:**

| Check | Sufficient Reason | SPEC/THEORY Reference |
|-------|-------------------|----------------------|
| Falsifiability coverage | §1.2 Measurability | INVARIANT_THEORY §1.2 |
| CQRS separation | §2.2 CQRS Separation Invariant | INVARIANT_THEORY §2.2 |
| Empty catch blocks | §1.1 Axiom of Determinism | INVARIANT_THEORY §1.1 |
| Source encoding | Encoding Invariant (this document) | INVARIANT_THEORY §1.2a |
| Startup feedback | User-Perceptual-Feedback Invariant | INVARIANT_THEORY §1.2a |
| Deviation inventory | §7.2 Deviation Protocol | COMPOSITUM_SPEC §7.2 |
| Plan compliance | §0.3 Plan Verification Protocol | COMPOSITUM_SPEC §0.3 |
| Recidivism self-audit | §1.2a Reflexive Measurability | INVARIANT_THEORY §1.2a |
| Build pass | §1.2 Measurability (build-time tooling) | INVARIANT_THEORY §1.2 |

### Axiom 4: Deterministic Reproducibility

Two runs of the exhaustive audit on the **same codebase state** must produce **identical results** (±0 violations).

**Formal:**
```
∀code_state s:
    exhaustive_audit(s, run_1).violations == exhaustive_audit(s, run_2).violations
```

This excludes heuristic, sampling, or probabilistic methods. All checks must be deterministic.

---

## Verification Method

1. Run `scripts/exhaustive-audit.ps1`
2. Record violation count
3. Run again without code changes
4. If counts differ, the audit is non-exhaustive (heuristic leakage)

## Popperian Criterion

```
F_r = {exhaustive-audit.ps1 that produces different violation counts on identical code}
E_r = {second run comparison: delta must be 0}
```

---

*Derived from INVARIANT_THEORY §1.1, §1.2, §1.2a, §2.2 and COMPOSITUM_SPEC §0.3, §7.2.*
