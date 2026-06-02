# Deviation Protocol 001: Component Scope Violation

**Status:** Active  
**Created:** 2026-06-02T15:45:00+05:00  
**Deadline:** 2026-06-09T23:59:59+05:00  
**Owner:** Agent Cascade  

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | INVARIANT_THEORY.md §2.3 Component Scope Invariant |
| **Location** | `Vantuz.Products.MinecraftLauncher.GUI` |
| **Nature** | Level 4 (Product) implements Level 2 (Plugin) interface |

## Technical Details

### Current State (Violating)
```
Namespace: Vantuz.Products.MinecraftLauncher.GUI
  ↓ Implements
Interface: ICommandPlugin (from Vantuz.Host)
  ↓ Belongs to
Level: Plugin (Category) = Level 2

Hierarchy Violation: Level 4 → Level 2 (bypasses Level 3, 2)
```

### Required State (Compliant)
```
Namespace: Vantuz.Plugins.GUI.MinecraftLauncher
  ↓ Implements  
Interface: ICommandPlugin
  ↓ Belongs to
Level: Plugin (Category) = Level 2

Hierarchy: Level 2 → Level 2 ✓ Valid
```

## Justification

**Why this deviation exists:**
1. Iterative refactoring approach: GUI moved from root to plugin first
2. Architectural reorganization pending: Products → Plugins migration requires coordinated changes
3. Current priority: functional launcher over strict hierarchy

**Why this is temporary:**
- Deviation deadline: 2026-06-09 (7 days)
- Resolution: Migrate to Vantuz.Plugins.GUI namespace
- No functional impact of violation, only architectural purity

## Resolution Plan

### Phase 1: Preparation (by 2026-06-04)
- [ ] Create Vantuz.Plugins.GUI.MinecraftLauncher project structure
- [ ] Update all namespace references
- [ ] Update boot.gui.json plugin references

### Phase 2: Migration (by 2026-06-07)
- [ ] Move all source files to new location
- [ ] Verify build succeeds
- [ ] Update documentation

### Phase 3: Cleanup (by 2026-06-09)
- [ ] Remove old Vantuz.Products.MinecraftLauncher.GUI
- [ ] Close this deviation protocol
- [ ] Update manifest.json

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Namespace confusion | Medium | Low | Clear documentation, temporary |
| Plugin loading failure | Low | High | Keep files identical during migration |
| Boot manifest breakage | Low | High | Update boot.gui.json atomically |

## Approval

**Deviation authorized by:** [Pending user confirmation]  
**Causal justification:** Iterative refactoring with strict deadline  
**Automatic escalation:** Warning on 2026-06-07, Error on 2026-06-09

---

*Per COMPOSITUM.md §4 Deviation Protocol and ARMATURA_DOCUMENT_PROTOCOL.md §9.4*
