---
version: 1.0
parent: INVARIANT_THEORY.md
parent_version: 1.1
---

# Deviation Protocol 008: Intentionally Omitted

**Status:** N/A — Number reserved, no deviation filed  
**Created:** 2026-06-08T00:00:00+05:00  
**Deadline:** N/A  
**Owner:** Agent Cascade  

---

## Rationale

Deviation number 008 was skipped in the sequential filing of deviation protocols during the 2026-06-03 session. No architectural violation requiring a deviation protocol was identified for this slot. The gap was discovered during the global compliance audit (compliance-audit-2026-06-08-global.md).

This placeholder ensures the deviation sequence remains contiguous and auditable per ARMATURA_DOCUMENT_PROTOCOL.md §1.1 (Axiom of Document Determinism).

## Verification

`F_doc`: `{docs/deviations/ without DEVIATION-008.md}`  
`E_doc`: `Get-ChildItem docs/deviations/DEVIATION-*.md | Sort-Object Name` — must return sequential list with 008 present.
