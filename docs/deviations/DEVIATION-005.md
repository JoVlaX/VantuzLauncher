# Deviation Protocol 005: Partial Loader Implementation

**Status:** Active — Phases 1–3 Resolved; Phase 4 (integration) Pending
**Created:** 2026-06-03T15:30:00+05:00
**Deadline:** 2026-06-30T23:59:59+05:00
**Owner:** Agent Cascade

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | COMPOSITUM_SPECIFICATION.md §4.2 Core Loader |
| **Location** | `Vantuz.Builder/PluginNameVerifier.cs` |
| **Nature** | Loader validates only plugin name identity (`Name` property), not full invariant `I(p)` |

## Technical Details

### Current State (Partial)

`PluginNameVerifier.cs:85-127` implements:
```
Loader_partial: Assembly → Name
where Name = plugin class Name property string
```

This verifies:
- ✅ `Name` property exists on concrete plugin types
- ✅ `Name` value matches pipeline `pluginName` references in `boot*.json`
- ✅ CQRS separation: `VerifyCQRS` implemented (ARM-BUILD-022)
- ✅ Resource category: `VerifyResources` implemented (ARM-BUILD-023)
- ✅ Scope restriction: `VerifyScope` implemented (ARM-BUILD-024)
- ❌ Does NOT verify `I(p) = Valid` as unified Loader (integration pending)

### Required State (Complete)

Per `COMPOSITUM_SPEC §4.2`:
```
Loader: Assembly → (I: Valid/Invalid)
where I(p) = I_CQRS(p) ∧ I_resource(p) ∧ I_scope(p)
```

Full invariant verification requires:
- **CQRS separation (`§2.2`):** No plugin implements both command and query interfaces
- **Resource category (`ARM010`):** `FileStream` uses only in UserManaged context
- **Component Scope (`§2.3`):** Product-level plugins do not reference Core internals directly

## Justification

**Why this deviation exists:**
1. Original scope (DEVIATION-002) was plugin name mismatch — not full invariant conformance
2. Mono.Cecil rewrite (Phase 7) solved the WPF dependency blocker for name discovery
3. Full `I(p)` verification requires significant Cecil method-body analysis expansion (method call graph inspection, interface implementation scanning)
4. Loader completeness is architectural roadmap item, not session-level deliverable

**Why this is temporary:**
- Deviation deadline: 2026-06-30 (27 days)
- Resolution: Extend `PluginNameVerifier` with additional invariant verifiers or separate `InvariantLoader` tool
- Partial Loader is sufficient for current build pipeline; full Loader needed before next major release

## Resolution Plan

### Phase 1: CQRS Separation Verification ✅ Resolved 2026-06-03
- [x] Extend `PluginNameVerifier` with `VerifyCQRS` inspecting `type.Interfaces` and method names for Command/Query mutual exclusion
- [x] Add `ARM-BUILD-022` error code for CQRS violation
- [x] Document falsifier set `F_r` and empirical test `E_r` in `verification-checklist.md`

### Phase 2: Resource Category Verification ✅ Resolved 2026-06-03
- [x] Add Cecil scan for `FileStream` / `HttpClient` / `Process` instantiation in plugin method bodies via `VerifyResources`
- [x] Classify resource usage against forbidden reference list
- [x] Add `ARM-BUILD-023` error code for resource category violation

### Phase 3: Component Scope Verification ✅ Resolved 2026-06-03
- [x] Add Cecil scan for cross-assembly references via `VerifyScope`
- [x] Validate `I_scope(p)` per `COMPOSITUM_SPEC §4.1` with allowed assembly whitelist
- [x] Add `ARM-BUILD-024` error code for scope violation

### Phase 4: Unified Loader Integration (by 2026-06-30)
- [ ] Integrate `VerifyCQRS`, `VerifyResources`, `VerifyScope` into unified `Loader: Assembly → (I: Valid/Invalid)` interface
- [ ] Generate single exit code from `PluginNameVerifier` aggregating all invariant checks
- [ ] Close this deviation protocol when `V_completeness_report.json` shows zero missing verifiers

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| False positives in CQRS detection | Medium | Medium | Manual review of flagged plugins before enforcing |
| Cecil scan performance degradation | Medium | Low | Cache scan results between builds |
| Scope checks too restrictive | Low | High | Whitelist pattern for legitimate cross-level references |

## Approval

**Deviation authorized by:** Agent Cascade (self-auditing per SCP-1)  
**Causal justification:** Full invariant verification exceeds current session scope; phased roadmap required per COMPOSITUM_SPEC §7.1  
**Automatic escalation:** Warning on 2026-06-10, Error on 2026-06-30

---

*Per COMPOSITUM.md §4 Deviation Protocol, INVARIANT_THEORY.md §9.4 Legacy Compatibility, and SCP-2 Symmetric Deadline Rule.*
