# Compositum

## Project Identity and Purpose

---

## 1. Project Identity

**What is Compositum:**

Compositum is a **universal composition mechanism** — not a framework, not a library, and not an end-user application. It is a system designed to create systems: a minimal, invariant foundation that enables the assembly of heterogeneous tools into unified solutions for unanticipated problems across arbitrary domains.

**Core Identity Statement:**

> Compositum exists as compositional potential. Its being is the capacity to combine, not the combination itself. The project is the possibility of synthesis, realized through strict adherence to invariant principles.

**What Compositum is NOT:**

- **Not a Framework** — does not mandate implementation patterns beyond invariant conformance
- **Not a Library** — does not expose APIs for direct consumption
- **Not an Application** — does not solve user-facing problems directly

**Analogy for Understanding:**

If traditional tools are musical instruments, Compositum is the conductor's system — not producing sound itself, but enabling the composition of sounds into coherent performance. The conductor (Host) knows nothing of violin or piano (plugins) individually, but understands the invariant language of composition.

---

## 2. Theoretical Foundation

**Parent Theory:**

Compositum is the first concrete realization of the **Armatura Invariant Theory** (`docs/theory/INVARIANT_THEORY.md`). All architectural decisions, constraints, and verification procedures derive from this theoretical foundation.

**Subordination Principle:**

Every component, plugin, and extension within Compositum must strictly satisfy the invariants defined in INVARIANT_THEORY. There are no exceptions without explicit, documented, and time-bounded deviation protocols.

**Key Theoretical Pillars Applied:**

| Pillar from INVARIANT_THEORY | Application in Compositum |
|------------------------------|----------------------------|
| **Measurability (§1.2)** | All plugins verified statically before runtime |
| **Compositional Being** | Existence as potential for synthesis |
| **Document Protocol** | Hierarchy governed by ARMATURA_DOCUMENT_PROTOCOL.md |
| **Nomadic Invariant (§3.2)** | Zero host-specific dependencies |
| **Agentic Execution (§11.5)** | Autonomous composition without domain knowledge |
| **CQRS Separation (§2.2)** | Clear command/query boundaries in plugins |

**Consequence:**

Any code accepted into Compositum must pass the verification function defined by INVARIANT_THEORY. This is non-negotiable and enforced at build-time.

---

## 3. Project Goals

**Primary Goal:**

Enable the creation of universal tools through strict invariant conformance, where components designed for one domain are inherently applicable to any domain sharing those invariants.

**Specific Objectives:**

1. **Universal Composition**
   - Any valid plugin must compose with any other valid plugin
   - Composition correctness determined by shared invariants, not domain knowledge

2. **Transdomain Applicability**
   - Plugins validated on one initiative must work on any initiative
   - No reimplementation when domain changes, only recomposition

3. **Agentic Generativity**
   - Autonomous agents can compose solutions without understanding problem semantics
   - Mechanical composition based on invariant compatibility alone

4. **Strict Verification**
   - Zero tolerance for invariant violations
   - Build-time verification as gatekeeper for all contributions

5. **Minimal Core**
   - Core system (Host, Pipeline, Loader) provides only essential composition infrastructure
   - No feature bloat; everything else belongs in plugin categories

**Success Criteria:**

A plugin created for initiative X (e.g., software distribution) can be recomposed for initiative Y (e.g., data processing) without modification, passing identical verification procedures.

---

## 4. Constraints & Boundaries

**What is FORBIDDEN in Compositum:**

| Constraint | Rationale | Consequence of Violation |
|------------|-----------|-------------------------|
| **Direct plugin-to-plugin interaction** | Violates Host-mediated architecture | Immediate rejection |
| **Domain-specific hardcoding** | Violates transdomain principle | Immediate rejection |
| **Implicit cross-category dependencies** | Violates isolation principle | Immediate rejection |
| **Runtime verification only** | Violates measurability axiom | Immediate rejection |
| **Deviation without protocol** | Violates scientific falsifiability | Immediate rejection |

**What is REQUIRED:**

1. **Static Verification**
   - All code must pass Roslyn analyzers enforcing INVARIANT_THEORY
   - No code enters repository without passing all checks

2. **Explicit Manifests**
   - All contextual values externalized to versioned manifests
   - No hardcoded paths, versions, or configuration

3. **Category Isolation**
   - Each plugin must function without other plugins from same category
   - Only Core is assumed available

4. **Deviation Protocol**
   - Any exception requires: TODO/FIXME marker, ISO8601 deadline, causal justification, named owner
   - Automatic escalation: Warning → Error as deadline approaches

**Agent-Specific Constraints:**

As an AI agent working with Compositum:

- You MUST NOT generate code that bypasses Host for plugin interaction
- You MUST NOT assume domain knowledge is required for composition
- You MUST verify all suggestions against INVARIANT_THEORY principles
- You MUST respect the hierarchy: Core → Category → Product
- You MUST NOT treat VantuzLauncher (or any category) as special — all categories are equally invariant-governed

---

## 5. Document Hierarchy

**Chain of Authority:**

```
Level 1: Universal Principles
    ├── INVARIANT_THEORY.md — Universal Armatura architectural principles
    └── ARMATURA_DOCUMENT_PROTOCOL.md — Documentation hierarchy governance
    └── Source of truth for ALL Armatura projects
    
Level 2: Project Specification  
    └── COMPOSITUM_SPECIFICATION.md — Project-specific invariant instantiation
    └── Located: docs/theory/COMPOSITUM_SPECIFICATION.md
    └── Subordinate to Level 1 documents
    
Level 3: Project Manifest
    └── COMPOSITUM.md (this document)
    └── High-level project identity and goals
    └── AI agent onboarding manifest
    
Level 4: Category Documentation
    └── VantuzLauncher/, Compositum.Test/, etc.
    └── Implementation details for specific plugin families
```

**Interaction Rules:**

1. **Upward Compliance:** Every level must satisfy constraints from all levels above
2. **Update Propagation:** Changes to Level N require verification of all levels N+1, N+2...
3. **No Sideways Dependencies:** Categories must not reference each other's specifics
4. **Deviation Chain:** Any deviation at Level N requires explicit markers at all affected levels below

**Your Position as AI Agent:**

You operate primarily at Level 3 (this document) and Level 4 (categories). When generating code or making architectural suggestions:

- Verify against Level 2 (COMPOSITUM_SPECIFICATION.md) for project-specific invariants
- Verify against Level 1 (INVARIANT_THEORY.md, ARMATURA_DOCUMENT_PROTOCOL.md) for universal principles
- Never assume Level 4 (categories) defines rules — they follow rules

---

## Summary for AI Agent

**Compositum in Three Principles:**

1. **Composition over Implementation** — We build the capacity to build, not the built
2. **Invariants over Instances** — Validity is structural, not domain-specific  
3. **Verification over Trust** — Nothing enters without proof, no exceptions without protocol

**Your Role:**

You are an agent of composition. You do not need to understand Minecraft, software distribution, or any specific domain. You need to understand invariants — and apply them invariantly.

**First Initiative:**

VantuzLauncher is the first validation of Compositum's transdomain claim. It is not special. It is merely first. What applies to VantuzLauncher applies to all future categories.

---

## Version

*Document: COMPOSITUM.md*  
*Location: Project Root*  
*Purpose: AI Agent Onboarding & Project Identity*  
*Version: 3.3.0*  
*Status: Active Manifest*  
*Hierarchy: Level 3 (see §5)*  
*Parent: COMPOSITUM_SPECIFICATION.md v3.2.0*  
*Ancestors: INVARIANT_THEORY.md v1.3, ARMATURA_DOCUMENT_PROTOCOL.md v1.3*
