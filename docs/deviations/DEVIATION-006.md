---
version: 1.0
parent: INVARIANT_THEORY.md
parent_version: 1.1
---

# Deviation Protocol 006: DAGVerifier Missing

**Status:** Resolved 2026-06-03  
**Created:** 2026-06-03T16:45:00+05:00  
**Deadline:** 2026-06-10T23:59:59+05:00  
**Closed:** 2026-06-03T17:30:00+05:00  
**Owner:** Agent Cascade  

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | COMPOSITUM_SPECIFICATION.md §7.1a V Completeness Dashboard |
| **Location** | `Vantuz.Builder/PipelineVisualizer.cs` |
| **Nature** | DAGVerifier not implemented; PipelineVisualizer constructs dependency graph but never detects cycles (`|C| = 0` not proven) |
| **ARM Code** | ARM-BUILD-021 |

## Technical Details

### Current State (Missing)

`PipelineVisualizer.cs:76-161` (`VisualizePipeline`) builds `dependencies`/`produces`/`consumes` dictionaries from boot manifest pipeline steps but contains no cycle detection algorithm.

### Expected State (per §2.1 Flow Invariant INVARIANT_THEORY)

Pipeline execution graph MUST be a DAG. Any cycle in the pipeline dependency graph violates the unidirectional flow invariant and would cause infinite execution loops at runtime.

```
ValidPipeline(P) ⟺ Pipeline(P) is DAG ⟺ |C| = 0
where C = {cycles in dependency graph}
```

## Phased Roadmap

| Phase | Deliverable | Deadline | Status |
|-------|-------------|----------|--------|
| 1 | Add `DetectCycle()` to PipelineVisualizer using Kahn's algorithm on existing `dependencies` graph | 2026-06-10 | ✅ Resolved 2026-06-03 |
| 2 | Integrate cycle detection into `verify` command path (return exit code 1 on cycle) | 2026-06-10 | ✅ Resolved 2026-06-03 |
| 3 | Add falsifier set documentation (ARM-BUILD-021) to verification-checklist.md | 2026-06-10 | ✅ Resolved 2026-06-03 |

## Justification (Causal Link)

DAG verification requires understanding of the full plugin interaction model (produces/consumes mapping). The current `GetProduces`/`GetConsumes` methods use hardcoded heuristics based on plugin name substrings, which may not generalize. Implementing a robust verifier requires either:
- Static attribute-based declaration (`[Produces("auth.token")]`, `[Consumes("username")]`)
- Runtime contract validation
- Heuristic-based detection with known limitations

A deviation is required to allow time for designing the correct static verification approach without breaking the build.

## Popperian Criterion

```
F_r = {PipelineVisualizer.cs without cycle detection}
E_r = {Read file + grep for "DetectCycle", "Kahn", "topological", "DFS", "cycle"}
```

Closure condition: `E_r` returns non-empty match (cycle detection implemented). ✅ Met 2026-06-03.

---

*Per INVARIANT_THEORY §9.4 Legacy Compatibility Theorem and §9.4a Symmetric Deadlines.*
