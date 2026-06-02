# DEVIATION-004: Auto-Fix Placeholder — Measurability Gap

**Status:** ACTIVE  
**Created:** 2026-06-02  
**Deadline:** 2026-06-09 (7 days)  
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

### Phase 1: Minimal Fix Engine (by 2026-06-05)
- [ ] Implement `Apply-CompilerFix` function in `auto-fix-orchestrator.ps1`
- [ ] Support top 5 patterns: missing semicolon (`CS1002`), missing brace (`CS1513`), missing namespace (`CS0246`), missing type (`CS0103`), missing member (`CS1061`)
- [ ] Use regex-based file editing with `-replace`
- [ ] Add unit tests for each pattern

### Phase 2: Hash Verification Hardening (by 2026-06-07)
- [ ] Ensure `Get-CodeHash` is deterministic (cross-platform canonical sorting)
- [ ] Add hash verification as separate test

### Phase 3: Closure (by 2026-06-09)
- [ ] Verify auto-fix applies at least one pattern successfully in CI
- [ ] Update this deviation document to `resolved`
- [ ] Remove placeholder comments from `Invoke-FixPhase`

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
