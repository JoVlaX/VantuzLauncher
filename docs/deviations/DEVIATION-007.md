# Deviation Protocol 007: NomadicVerifier / Transdomain Primitive Static Verification Missing

**Status:** Resolved 2026-06-03  
**Created:** 2026-06-03T16:45:00+05:00  
**Deadline:** 2026-06-30T23:59:59+05:00  
**Closed:** 2026-06-03T17:35:00+05:00  
**Owner:** Agent Cascade  

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | COMPOSITUM_SPECIFICATION.md §5.1 Transdomain Primitives / INVARIANT_THEORY §3.2 Nomadic Invariant |
| **Location** | `Vantuz.Builder/PluginNameVerifier.cs` |
| **Nature** | NomadicVerifier not implemented; transdomain primitives (`ArtifactVersioning`, `DependencyResolution`) have no static verification mechanism |
| **ARM Code** | ARM-BUILD-026 |

## Technical Details

### Current State (Missing)

`PluginNameVerifier.cs:85-128` (`DiscoverPluginNames`) inspects plugin assemblies for `Name` property only. No scan exists for:
- `[TransdomainPrimitive]` attributes on types
- Cross-category dependency references (host-specific code in plugins)
- Primitive contracts that must work across all Compositum categories

### Expected State (per §3.2 Nomadic Invariant)

```
NomadicPlugin(p) ⟺ ∀host ∈ Hosts: p.functional(host) = true
TransdomainPrimitive(t) ⟺ ∀category ∈ Categories: t.usable(category) = true
```

Static verification must detect host-specific code (e.g., `Windows.Forms`, `AspNetCore`, platform-specific P/Invoke) inside plugin assemblies.

## Phased Roadmap

| Phase | Deliverable | Deadline | Status |
|-------|-------------|----------|--------|
| 1 | Design `[TransdomainPrimitive]` attribute and host-specific forbidden reference list | 2026-06-15 | ✅ Resolved 2026-06-03 |
| 2 | Implement Cecil-based scan for forbidden references in plugin assemblies | 2026-06-25 | ✅ Resolved 2026-06-03 |
| 3 | Add falsifier set documentation (ARM-BUILD-026) to verification-checklist.md | 2026-06-30 | ✅ Resolved 2026-06-03 |

## Justification (Causal Link)

Nomadic invariant verification requires a complete catalog of:
1. All host-specific APIs (framework-dependent, platform-specific)
2. All transdomain primitives defined in COMPOSITUM_SPEC §5.1
3. A mapping between plugin types and their portability contracts

This catalog does not yet exist in the codebase. Building it requires analysis of all existing plugins and their actual host dependencies, which exceeds the scope of a single session. The deviation allows systematic catalog construction while keeping the build functional.

## Popperian Criterion

```
F_r = {PluginNameVerifier.cs without forbidden reference scan}
E_r = {Read file + grep for "VerifyNomadic", "ForbiddenHostSpecificTypes", "P/Invoke"}
```

Closure condition: `E_r` returns non-empty match (nomadic verification implemented). ✅ Met 2026-06-03.

---

*Per INVARIANT_THEORY §9.4 Legacy Compatibility Theorem and §9.4a Symmetric Deadlines.*
