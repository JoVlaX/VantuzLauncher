---
version: 1.0
parent: COMPOSITUM_SPECIFICATION.md
parent_version: 3.3.0
---

# Deviation Protocol META-005: Code-Driven Inference Prohibition Violation

**Status:** Resolved 2026-06-10
**Created:** 2026-06-08T16:20:00+05:00
**Deadline:** 2026-06-15T23:59:59+05:00
**Owner:** Agent Cascade

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | COMPOSITUM_SPECIFICATION.md §9.2 Code-Driven Inference Prohibition |
| **Location** | IGameProvider CQRS violation detection (Session 1) |
| **Nature** | Agent inferred architectural violation from code before reading specification |

## Technical Details

### Current State (Violating)
```
Detection method:
  - read_file(Contracts.cs)
  - Observed: CheckVersionAsync (read) + InstallVersionAsync (write) in same interface
  - Conclusion: CQRS violation
  - Theory read: After detection
```

### Required State (Compliant)
```
Detection method:
  - read_file(COMPOSITUM_SPECIFICATION.md §4.1, §2.2)
  - Understand: CQRS separation is mandatory for Plugin-level components
  - read_file(Contracts.cs)
  - Verify: IGameProvider matches theoretical criteria for violation
  - Conclusion: Theory-predicted violation confirmed by code
```

## Justification

**CausalLink:** COMPOSITUM_SPEC §9.2 states that agents MUST NOT infer architecture solely from implementation code when a higher-level specification exists. While the inference was correct in this case, relying on code-first detection creates risk of false positives (e.g., detecting CQRS violations in ExternalAbstraction components where scope rules differ).

## Remediation

1. Enforce theory-first workflow: read spec before inspecting code for architectural violations.
2. Document: code inspection is for evidence gathering, not violation detection.
3. Add a session log check: first read of specification must precede first architectural violation claim.

## Falsifier

Agent detects architectural violation from code before reading the specification that defines the violation.

## Resolution

Same `workflow-theory-first-6010f6.md` enforces §9.2 Code-Driven Inference Prohibition: code inspection is for evidence gathering, not violation detection. Rule: first read of specification must precede first architectural violation claim. Forbidden sequence (code → inference → theory) explicitly documented.

**Closed:** 2026-06-10T17:00:00+05:00

## E_doc

`workflow-theory-first-6010f6.md` contains §9.2 enforcement rule: "Code inspection is for evidence gathering, not violation detection."
