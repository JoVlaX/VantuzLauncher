---
version: 1.0
parent: COMPOSITUM_SPECIFICATION.md
parent_version: 3.3.0
---

# Deviation Protocol META-004: Theory-First Execution Violation

**Status:** Open
**Created:** 2026-06-08T16:20:00+05:00
**Deadline:** 2026-06-15T23:59:59+05:00
**Owner:** Agent Cascade

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | COMPOSITUM_SPECIFICATION.md §9.1 Theory-First Execution |
| **Location** | `Contracts.cs` IGameProvider split (Session 1) |
| **Nature** | Structural change proposed without citing §4.1 and §2.2 |

## Technical Details

### Current State (Violating)
```
Agent action sequence:
  1. read_file(Contracts.cs) — inferred CQRS violation from code
  2. edit(Contracts.cs) — split IGameProvider
  3. read_file(COMPOSITUM_SPECIFICATION.md) — read theory AFTER change

Citations: None before first edit
```

### Required State (Compliant)
```
Agent action sequence:
  1. read_file(COMPOSITUM_SPECIFICATION.md §4.1, §2.2)
  2. Cite: "Per §4.1 Component Scope Invariant and §2.2 CQRS Separation..."
  3. read_file(Contracts.cs)
  4. edit(Contracts.cs)
```

## Justification

**CausalLink:** COMPOSITUM_SPEC §9.1 requires any agent proposing structural changes to cite §4.1 and §2.2 BEFORE execution. The IGameProvider split was the most impactful structural change of the audit, yet it was driven by code inspection rather than theory citation. The result was correct, but the method violated the protocol.

## Remediation

1. Add a mandatory pre-edit step: read and cite relevant theory sections.
2. Update future plans to include theory citations as prerequisites.
3. Create a check: no structural edit without preceding theory citation in session log.

## Falsifier

Session log shows structural edit before theory citation.

## E_doc

Search trajectory for `IGameProvider` split edit timestamp vs. first `COMPOSITUM_SPECIFICATION.md` read timestamp.
