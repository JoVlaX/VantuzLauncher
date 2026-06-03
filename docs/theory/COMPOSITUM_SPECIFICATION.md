# Compositum Specification

## A Universal Compositional System for Tool Synthesis

---

## Abstract

This document presents the ontological and architectural constitution of Compositum — the first reference implementation of the Armatura invariant theory. Compositum establishes a minimal, invariant foundation for composing heterogeneous tools into unified systems capable of solving unanticipated problems across arbitrary domains. The theory is grounded in the Compositional Being invariant, transdomain applicability, and agentic generativity, with all components strictly subordinate to INVARIANT_THEORY.

**Keywords:** compositional being, plugin categories, transdomain applicability, agentic execution, universal composition, VantuzLauncher

---

## Document Hierarchy and Interaction Rules

### §0.1 Hierarchy of Documents

**Statement:** Compositum project documentation follows a strict four-level hierarchical dependency chain governed by Armatura principles and document protocol.

**Formalization:**
```
Hierarchy (source of truth order):
    Level 1: INVARIANT_THEORY.md — Universal Armatura principles
             ARMATURA_DOCUMENT_PROTOCOL.md — Universal documentation governance
             
    Level 2: COMPOSITUM_SPECIFICATION.md (this document) — Project-specific instantiation
             Subordinate to: INVARIANT_THEORY.md, ARMATURA_DOCUMENT_PROTOCOL.md
             
    Level 3: COMPOSITUM.md (root) — AI agent onboarding manifest
             Subordinate to: COMPOSITUM_SPECIFICATION.md
             
    Level 4: Category-specific documentation (VantuzLauncher, etc.)
             Subordinate to: COMPOSITUM.md

Dependency: Level(n) strictly subordinate to Level(n-1)
Invariance: Level(n).valid ⟹ Level(n-1).unchanged OR Level(n).updated_to_match
```

**Justification (§10.3 Compositionality INVARIANT_THEORY):** Hierarchical decomposition achieves minimum description length across the documentation system. Separation of universal principles (L1) from project instantiation (L2) enables reuse while maintaining strict subordination.

**Popperian Criterion:**
```
F_r = {Level(n) document without explicit parent reference to Level(n-1)}
E_r = {manifest.json validation: parent_version field exists and matches actual parent version}
```

### §0.2 Update Protocol

**Statement:** Changes to Level 1 documents (INVARIANT_THEORY.md, ARMATURA_DOCUMENT_PROTOCOL.md) trigger mandatory verification cascade to Level 2 (this document).

**Formalization:**
```
CascadingUpdate(Parent, Δ):
    Preconditions:
        1. Parent ∈ {INVARIANT_THEORY.md, ARMATURA_DOCUMENT_PROTOCOL.md}
        2. Δ is explicit with CausalLink(Δ, architectural_decision)
        3. V(Parent)' = bumped per ARMATURA_DOCUMENT_PROTOCOL.md §3.1
    
    Verification:
        Let Affected = {COMPOSITUM_SPECIFICATION.md}
        
        ∀doc ∈ Affected:
            compliance = VerifySubordination(doc, Parent')
            
            Case Compliant:
                V(doc)' = bump_minor(V(doc))
                Update doc.parent_version = V(Parent)'
                Mark: SYNC_COMPLETE
                
            Case Violation:
                Report: HIERARCHY_VIOLATION(doc, Parent', violation_details)
                Options:
                    a) Fix doc to restore compliance → proceed
                    b) File DeviationProtocol(doc, violation, deadline, justification) per §7.2
                
    Postconditions:
        ∀doc ∈ Affected:
            doc.parent_version = V(Parent)' ∨
            DeviationProtocol active for doc
```

**Justification (§11.4 Context Evolution Protocol INVARIANT_THEORY):** Parent changes MUST propagate explicitly to children. Silent non-compliance violates temporal falsifiability.

**Popperian Criterion:**
```
F_r = {Level 2 document with outdated parent reference after Level 1 update}
E_r = {agent pre-write check: parent_version matches manifest}
```

### §0.3 Plan Verification Protocol

**Statement:** All agent-generated plans MUST pass structural validation before execution.

**Formalization:**
```
Plan(p) valid ⟺
    ∀section ∈ p.analysis:
        section.claims ⊂ VerifiableClaims(F_doc, E_doc) ∧
    ∀action ∈ p.actions:
        ∃Deadline(a): a ∈ ISO8601 ∧
    ∃SelfAudit(p): V_self(p) = Valid
```

**Checklist:**
- [ ] Does the plan analyze artifacts against INVARIANT_THEORY? If yes, does it include `## Meta-Compliance`?
- [ ] Does every proposed action have an ISO8601 deadline?
- [ ] Are all claims file-system verifiable or marked [HYPOTHESIS]?
- [ ] Does the plan include a `## Self-Audit` section?

**Justification (§1.2a Reflexive Measurability INVARIANT_THEORY):** Plans asserting compliance with Armatura must themselves be demonstrably compliant.

### §0.4 Document Responsibilities

**Statement:** Each document in the hierarchy serves a distinct epistemological function with measurable compliance criteria.

**Formalization:**
```
Let Responsibilities: Document → {purpose, audience, strictness, parent}

Responsibilities(INVARIANT_THEORY) = 
    ⟨universal principles, all Armatura projects, immutable, null⟩
    
Responsibilities(ARMATURA_DOCUMENT_PROTOCOL) =
    ⟨documentation governance, all Armatura projects, immutable, null⟩
    
Responsibilities(COMPOSITUM_SPECIFICATION) =
    ⟨project-specific instantiation, implementers/core, subordinate, {INVARIANT_THEORY, DOCUMENT_PROTOCOL}⟩
    
Responsibilities(COMPOSITUM_ROOT) =
    ⟨project identity/onboarding, AI agents/contributors, subordinate, COMPOSITUM_SPECIFICATION⟩
    
Responsibilities(Category_Docs) =
    ⟨implementation details, plugin developers, subordinate, COMPOSITUM_ROOT⟩

Compliance(doc) ⟺ doc.content satisfies Responsibilities(doc).strictness
```

**Table Summary:**

| Document | Purpose | Audience | Strictness | Parent |
|----------|---------|----------|------------|--------|
| INVARIANT_THEORY.md | Universal architectural principles | All Armatura projects | Immutable | — |
| ARMATURA_DOCUMENT_PROTOCOL.md | Documentation governance protocol | All Armatura projects | Immutable | — |
| COMPOSITUM_SPECIFICATION.md | Compositum-specific invariants | Implementers, Core | Subordinate | L1 documents |
| COMPOSITUM.md (root) | Project identity/onboarding | AI agents, Contributors | Subordinate | SPECIFICATION |
| Category docs | Implementation details | Plugin developers | Subordinate | COMPOSITUM.md |

**Justification (§2.3 Component Scope Invariant INVARIANT_THEORY):** Rules apply selectively based on component position in architectural hierarchy. Level 1 principles are universal; Level 2 instantiates for specific project.

**Popperian Criterion:**
```
F_r = {document violating its defined Responsibilities.strictness}
E_r = {manifest validation: level, parent, and strictness fields match actual document content}
```

---

## §0.4 Protocol Reference

**Statement:** Practical document hierarchy management is governed by `ARMATURA_DOCUMENT_PROTOCOL.md`.

**Formalization:**
```
Let Dₙ = Document at Level n in Compositum project
Let Protocol = ARMATURA_DOCUMENT_PROTOCOL.md

Governance: ∀Dₙ where n ∈ {2,3,4}:
    Dₙ.versioning follows Protocol.§3
    Dₙ.cascading follows Protocol.§4  
    Dₙ.compatibility follows Protocol.§5
    Dₙ.verification follows Protocol.§6
    Dₙ.validation follows Protocol.§7
```

**Justification (§10.3 Compositionality INVARIANT_THEORY):** Reusable protocol definitions minimize redundancy across Armatura projects while maintaining strict conformance.

**Popperian Criterion:**
```
F_r = {Dₙ managing hierarchy without referencing Protocol}
E_r = {manifest validation: parent_version references Protocol}
```

---

## §1. Foundational Axioms

### §1.1 Axiom of Armatura Subordination

**Statement:** Compositum is a concrete realization of Armatura principles. All invariants derive from `docs/theory/INVARIANT_THEORY.md`.

**Formalization:**
```
Let A = Armatura invariant set from INVARIANT_THEORY
Let C = Compositum component set

Subordination: ∀c ∈ C: c satisfies A
```

**Justification (System Principle):** Without unified theoretical foundation, Compositum would constitute an ad-hoc system lacking epistemological coherence with the Armatura brand.

**Popperian Criterion:**
```
F_r = {code in C that violates any rule from INVARIANT_THEORY}
E_r = {Roslyn analyzers enforcing INVARIANT_THEORY rules}
```

---

## §2. Ontological Definition

### §2.1 Statement of Being

**Statement:** Compositum exists as a universal composition mechanism, not as a concrete tool.

**Formalization:**
```
Let ToolSpace = {frameworks, libraries, applications}
Let MechanismSpace = {composition engines, orchestrators}

Ontology: Compositum ∈ MechanismSpace
         Compositum ∉ ToolSpace

∀t ∈ ToolSpace: t solves specific problem p ∈ Problems
Compositum: ∃Composition: 2^ToolSpace → System
    where System solves arbitrary p ∈ Problems through composition
```

**Justification (Occam's Razor §7.1 INVARIANT_THEORY):**
- Monolithic approach: O(|Problems|) implementations required
- Compositional approach: O(|ToolSpace|) where |ToolSpace| << |Problems| (combinatorial coverage)
- Minimum Description Length achieved through universal composition

**Popperian Criterion (§1.2 INVARIANT_THEORY):**
```
F_r = {code implementing concrete problem-solving without compositional abstraction}
E_r = {static analysis detecting direct problem-domain coupling}
```

### §2.2 Negative Ontology (What Compositum Is Not)

**Statement:** Compositum is neither framework, library, nor application.

**Formalization:**
```
∀c ∈ Compositum:
    ¬(∃API: c exposes API for direct consumption)           [¬library]
    ¬(∃Pattern: c mandates implementation patterns)         [¬framework]
    ¬(∃UserProblem: c solves user-facing problem directly)    [¬application]
```

**Popperian Criterion:**
```
F_r = {
    public API surface without compositional context,
    mandatory inheritance from non-invariant base classes,
    direct user interaction code in Core
}
E_r = {architectural boundary tests}
```

---

## §3. Compositional Being Invariant

### §3.1 Statement

**Statement:** The being of Compositum is its capacity for composition. The project exists not as code, but as compositional potential.

**Formalization:**
```
Let P = {p₁, p₂, ...} — plugin set
Let I: P → {Valid, Invalid} — invariant verification function (I = INVARIANT_THEORY)
Let D = {d₁, d₂, ...} — problem domain set
Let System(d) — solution for domain d

Compositional Being Invariant:
    (1) ∀p ∈ P: I(p) = Valid                      [invariant conformance]
    (2) ∃Composition: 2^P → (D → System)           [composition function]
    (3) ∀d ∈ D, ∃C ⊆ P: Composition(C)(d) ≠ ∅      [universal coverage]
    (4) ∀C₁, C₂ ⊆ P: C₁ ∩ C₂ ≠ ∅ → composable(C₁ ∪ C₂)  [shared invariants enable composition]
```

**Justification (Occam's Razor):** Code-as-being duplicates context in every component. Potential-as-being externalizes context to compositional function, achieving MDL across the system.

### §3.2 Corollaries

**Corollary 1 — Self-Sufficiency:**
```
∃C₀ ⊆ P: |C₀| = 1 ∧ Composition(C₀) ≠ ∅
```
Compositum is functional even with single plugin (reflexivity).

**Corollary 2 — Transdomain Applicability (§3.2 Nomadic INVARIANT_THEORY):**
```
∀p ∈ P, ∀d₁, d₂ ∈ D: I(p) = Valid → (p usable in d₁ ↔ p usable in d₂)
```
Invariant I decouples plugin from originating initiative. Plugin created for d₁ is applicable in d₂, d₃...

**Corollary 3 — Agentic Generativity (§11.5 INVARIANT_THEORY):**
```
∀Agent A, ∀d ∈ D: A can generate C ⊆ P solving d
```
Agent without domain semantics can compose plugins mechanically because I defines compatibility structurally.

**Popperian Criterion for Corollary 3:**
```
F_r = {agent requiring domain knowledge to compose plugins}
E_r = {automated composition test: agent generates valid C without manual intervention}
```

---

## §4. Architectural Hierarchy

### §4.1 Component Scope Invariant (§2.3 INVARIANT_THEORY)

**Statement:** Components exist at distinct hierarchical levels with invariant applicability.

**Formalization:**
```
Let H = {Core, Category, Product} — hierarchy levels
Let Scope: Invariants → P(H) — invariant applicability mapping

∀inv ∈ INVARIANT_THEORY, ∀c ∈ Compositum:
    Applies(inv, c) ↔ Level(c) ∈ Scope(inv)

Hierarchy:
    Core = {Host, Pipeline, Loader}
    Category = {VantuzLauncher, Compositum.Test, ...}
    Product = {specific initiative compositions}
```

### §4.2 Compositum Core

**Statement:** Minimal invariant foundation enabling composition.

**Formalization:**
```
Core = {
    Host: Lifecycle × Resources → ManagedExecution,
    Pipeline: DAG(Operations) → ExecutionPlan,
    Loader: Assembly → (I: Valid/Invalid)
}

Minimality: ¬∃c ∈ Core: Core \ {c} still satisfies Compositional Being
```

**Justification (Occam's Razor):** Any additional component in Core would violate minimal description length without adding compositional capacity.

### §4.3 Plugin Category Definition

**Statement:** Category is a plugin set unified by common idea and invariant compatibility.

**Formalization:**
```
Category = {c ⊂ P | ∀p₁, p₂ ∈ c: Idea(p₁) = Idea(p₂) ∧ I(p₁) = I(p₂) = Valid}

DomainConstraint: Category is not bound to specific domain d ∈ D
```

**Popperian Criterion:**
```
F_r = {category with hardcoded domain dependency}
E_r = {static analysis detecting domain-specific imports in category code}
```

---

## §5. Plugin Categories

### §5.1 VantuzLauncher Category

**Statement:** VantuzLauncher is the first realized category — plugins for software distribution.

**Formalization:**
```
VantuzLauncher = {p ∈ P | Idea(p) = "software distribution" ∧ I(p) = Valid}

Initiative: Minecraft distribution (specific d ∈ D for category validation)
Transdomain Primitives:
    ∀p ∈ VantuzLauncher: ArtifactVersioning(p) ∨ DependencyResolution(p) ∨
                          DeltaUpdate(p) ∨ InstallationValidation(p)
```

**Justification (Transdomain):** Primitives above are invariant relative to target software. Minecraft validates; any software applies.

### §5.2 Compositum.Test Category (Proposed)

**Statement:** Universal testing category for verifying arbitrary compositions.

**Formalization:**
```
Compositum.Test = {p ∈ P | Idea(p) = "composition verification" ∧ I(p) = Valid}

Unit of Testing: Composition C ⊆ P (not individual plugin)
Test: C → {Pass, Fail} based on invariant conformance and functional correctness

Meta-Invariant: Compositum.Test ⊂ P → Compositum.Test itself testable by Compositum.Test
```

**Popperian Criterion:**
```
F_r = {tests requiring domain knowledge, non-deterministic tests, host-dependent tests}
E_r = {CI execution across multiple hosts with identical results}
```

---

## §6. Boundary Conditions

### §6.1 Allowed Operations

**Statement:** Operations permitted within Compositum boundaries.

**Formalization:**
```
Allowed = {
    AddPlugin: P × I → P ∪ {p_new},
    CreateCategory: Idea × P → Category,
    ExtendCore: Core → Core' where I(Core') = Valid ∧ backwards_compatible(I)
}
```

### §6.2 Forbidden Operations (Negative Constraints)

**Statement:** Operations prohibited by Compositional Being.

**Formalization:**
```
Forbidden = {
    DirectPluginInteraction: p₁ × p₂ → Result (bypassing Host),
    DomainCoupling: Category → d (hardcoding domain),
    ImplicitDependency: p₁ → p₂ where p₁, p₂ ∈ different categories,
    InvariantViolation: ∀p: I(p) = Invalid
}

∀f ∈ Forbidden: ¬∃c ∈ Compositum: f implemented in c
```

**Popperian Criterion:**
```
F_r = {code using direct method calls between plugins,
       domain-specific strings in category code,
       plugin referencing another plugin's concrete type}
E_r = {Roslyn analyzers detecting forbidden patterns}
```

### §6.3 Plugin Acceptance Criteria

**Statement:** Necessary and sufficient conditions for plugin inclusion.

**Formalization:**
```
Accept(p, category) ↔
    I(p) = Valid ∧                                    [invariant check]
    Idea(p) = Idea(category) ∧                        [category coherence]
    Independent(p, category \ {p})                     [isolation]

where Independent(p, S) = p functional without any s ∈ S
```

**Popperian Criterion:**
```
F_r = {plugin failing static checks, plugin not functioning standalone}
E_r = {CI pipeline enforcing all three criteria}
```

---

## §7. Compliance & Falsifiability

### §7.1 Mandatory Verification

**Statement:** All code must pass INVARIANT_THEORY verification before acceptance.

**Formalization:**
```
∀code ∈ Compositum: 
    ∃V: Code → {Valid, Invalid}: V(code) = Valid ⟹ code ∈ Compositum
    where V = conjunction of all verifiers from INVARIANT_THEORY
```

#### §7.1a V Completeness Dashboard

**Statement:** `V = ∧V_i` must be operationally checkable at build time.

**Formalization:**
```
V_complete ⟺ |V_implemented| = |V_required| ∧ ∀v ∈ V_required: v ∈ V_implemented

V_required = {
    NameVerifier, CQRSVerifier, ResourceVerifier,
    ScopeVerifier, DAGVerifier, NomadicVerifier
}
```

**Build requirement:** Every build MUST output `V_completeness_report.json` listing:
- Implemented verifiers with ARM codes
- Required but unimplemented verifiers
- Deviation protocol status for any missing verifier

A missing verifier without active deviation protocol is a **build error**.

### §7.2 Zero-Tolerance Policy

**Statement:** No deviation from INVARIANT_THEORY is permitted without explicit deviation protocol.

**Formalization:**
```
Deviation(e, code, rule) valid ↔
    ∃Marker: m ∈ {TODO, FIXME, pragma warning disable} ∧
    ∃Deadline: d ∈ ISO8601 ∧ d > Now() ∧
    ∃Justification: CausalLink(j, rule) ∧
    ∃Owner: o ∈ TeamMembers

Severity(e) = {Warning if Now() < d - 30 days, Error if Now() ≥ d}
```

**Reference:** §9.4 Legacy Compatibility Theorem (INVARIANT_THEORY)

### §7.3 Continuous Verification

**Statement:** Verification occurs at build-time, not runtime.

**Formalization:**
```
∀code: Build(code) succeeds ↔ V_build(code) = Valid
where V_build includes:
    - Roslyn analyzers (INVARIANT_THEORY rules)
    - Architectural boundary tests
    - Composition validity checks
```

---

## §8. Relation to INVARIANT_THEORY

### §8.1 Theoretical Grounding

Compositum instantiates INVARIANT_THEORY principles:

| INVARIANT_THEORY | Compositum Realization |
|------------------|------------------------|
| §1.2 Measurability | All plugins statically verified before loading |
| §2.1 Flow Invariant | DAG Pipeline in Core ensures unidirectional execution |
| §2.2 CQRS | Category plugins separate queries from commands |
| §3.2 Nomadic Invariant | No host-specific code in any category |
| §11.5 Agentic Execution | Composition(C) executable by autonomous agents |
| §12.3 Namespace Correspondence | Category structure reflects filesystem hierarchy |

### §8.2 Meta-Invariance

**Statement:** Compositum itself evolves while preserving valid code validity (§11.1 INVARIANT_THEORY).

**Formalization:**
```
∀code ∈ Compositum, ∀tᵢ, tⱼ:
    Valid(code, Context(tᵢ)) → Valid(code, Context(tⱼ))
    where Context(tⱼ) = Evolution(Context(tᵢ), ΔINVARIANT_THEORY)
```

---

## Conclusion

Compositum represents a **scientific architectural realization** grounded in:
- **Compositional Being** — existence as compositional potential
- **Transdomain Applicability** — plugins work beyond originating initiatives
- **Agentic Generativity** — autonomous composition without domain knowledge
- **Strict Subordination** — all components verified against INVARIANT_THEORY

The system is falsifiable: any component violating INVARIANT_THEORY is automatically rejected by build-time verification.

---

## Version

*Document: COMPOSITUM_SPECIFICATION.md*  
*Version: 3.3.0*  
*Status: Formalized Project Specification*  
*Parent: INVARIANT_THEORY.md v1.1, ARMATURA_DOCUMENT_PROTOCOL.md v1.3*  
*Sibling: COMPOSITUM.md (root AI manifest)*  
*Changes: Added §0.3 Plan Verification Protocol, §7.1a V Completeness Dashboard; Renumbered §0.3→§0.4 Document Responsibilities; Parent updated to INVARIANT_THEORY v1.1 (Reflexive Measurability, Document Falsifiability, Symmetric Deadlines)*
