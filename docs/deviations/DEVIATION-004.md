---
version: 1.0
parent: INVARIANT_THEORY.md
parent_version: 1.1
---

# DEVIATION-004: Auto-Fix Placeholder — Measurability Gap

**Status:** Resolved 2026-06-03  
**Created:** 2026-06-02  
**Deadline:** 2026-06-09 (7 days)  
**Closed:** 2026-06-03  
**Type:** Measurability / Functional Gap  
**Severity:** MEDIUM — Auto-fix orchestrator cannot apply measurable code changes

---

## Violation Description

Per INVARIANT_THEORY.md §1.2 Axiom of Measurability, every state must be statically verifiable. The `Invoke-FixPhase` function in `auto-fix-orchestrator.ps1` is a placeholder: it detects known error patterns but delegates actual code modification to external tools, which are not integrated. As a result, the hash-before vs hash-after check always fails (`$hashAfter -eq $hashBefore`), and the orchestrator reports `Fixed = $false` on every attempt.

**Rule violated:** §1.2 Measurability — the fix phase does not produce a measurable state change.

**Location:** `auto-fix-orchestrator.ps1`, lines 230-270 (`Invoke-FixPhase`)

---

## Technical Details

### Current State (Violating)
```powershell
# Note: Actual fix implementation requires code analysis and modification
# This orchestrator manages the loop; fixes are applied by code analysis tools

$hashAfter = Get-CodeHash
if ($hashAfter -eq $hashBefore) {
    return @{ Fixed = $false; Reason = "No code change produced" }
}
```

**Behavior:**
- Error patterns are matched correctly (`Test-CanAutoFix`)
- No actual file modification occurs
- Hash comparison always fails
- Orchestrator aborts with `Fixed = $false`

### Required State (Compliant)
```powershell
# Apply regex-based or AST-based fixes for known compiler errors
$patchResult = Apply-CompilerFix -Pattern $canFix.Pattern -File $buildResult.File
if (-not $patchResult.Success) { ... }

$hashAfter = Get-CodeHash
if ($hashAfter -eq $hashBefore) {
    return @{ Fixed = $false; Reason = "No code change produced" }
}
```

**Behavior:**
- Detect error pattern
- Apply targeted code modification
- Verify hash changed
- Retry build/test cycle

---

## Justification

**Why this deviation exists:**
1. Scope boundary: The orchestrator's responsibility is loop management, not code analysis
2. Code analysis requires AST parsing (Roslyn) or regex heuristics — significant additional scope
3. Current priority: stabilize build/test pipeline before adding AI-driven fixes
4. The placeholder still satisfies §7.1 Mandatory Verification (falsification test exists — it always correctly reports failure)

**Why this is temporary:**
- Deadline: 2026-06-09 (7 days)
- Resolution: Integrate minimal regex-based fix engine for top 5 compiler errors
- Alternative: Integrate with external code analysis API

---

## Resolution Plan

### Phase 1: Minimal Fix Engine ✅ Resolved 2026-06-03
- [x] Implement `Repair-MissingBootJson` and `Add-MissingUsingDirective` functions in `auto-fix-orchestrator.ps1`
- [x] Support concrete patterns: missing boot.json (test error), missing namespace (`CS0246`/`CS0234`)
- [x] Use regex-based file editing with `-replace` and `Set-Content`
- [x] `Get-CodeHash` extended to include `.json` files for measurable state change

### Phase 2: Hash Verification Hardening ✅ Resolved 2026-06-03
- [x] Ensure `Get-CodeHash` is deterministic (cross-platform canonical sorting via `ToLowerInvariant`)
- [x] Hash now covers `.cs` and `.json` files

### Phase 3: Closure ✅ Resolved 2026-06-03
- [x] Verified auto-fix dispatches concrete repair functions
- [x] Updated this deviation document to `resolved`
- [x] Replaced placeholder comments with concrete fix dispatch in `Invoke-FixPhase`

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Regex fixes produce invalid code | Medium | High | Limit to 5 well-tested patterns; abort on syntax error |
| Fix engine never implemented | Low | Medium | Hard deadline with escalation on 2026-06-07 |
| False positives in pattern matching | Low | Medium | Require exact regex match; no fuzzy fixes |

---

## Approval

**Deviation authorized by:** Agent Cascade (per COMPOSITUM.md §4)  
**Causal justification:** Orchestrator scope separation — loop management vs code analysis  
**Automatic escalation:** Warning on 2026-06-07, Error on 2026-06-09

---

*Per COMPOSITUM.md §4 Deviation Protocol and ARMATURA_DOCUMENT_PROTOCOL.md §9.4*
