# Invariant Theory of Armatura

## A Compositional, Falsifiable, and Ergodic Foundation for Architectural Constitutions

---

## Abstract

This document presents a formal mathematical theory establishing the epistemological and ontological foundations of the Armatura architectural constitution. The theory is grounded in Popperian falsifiability, invariance principles, compositional algebra, Occam's razor, causality, and ergodic theory. It provides rigorous justification for why Armatura rules exist in their specific forms and how they collectively ensure system correctness, maintainability, and nomadic portability.

**Keywords:** architectural invariants, compositional algebra, falsifiability, nomadic computing, CQRS, static verification, meta-invariance, agentic execution

---

## 1. Foundational Axioms

### 1.1 Axiom of Determinism (Temperature = 0)

**Statement:** The interpretation engine operates in a temperature=0 regime, selecting single unambiguous interpretations.

**Formalization:**
```
∀SystemStates S, S' ∈ Σ: 
    Interpretation(S) = argmin_{i ∈ ValidInterpretations(S)} Complexity(i)
    
where Complexity(i) = |i| + λ·Entropy(i)
```

**Justification (Occam's Razor):** When multiple valid implementations exist, the system MUST select the simplest by Kolmogorov complexity. This corresponds to the Minimum Description Length (MDL) principle.

### 1.2 Axiom of Measurability

**Statement:** All rules MUST be enforceable by build-time tooling, tests, or deterministic static validation.

**Formalization:**
```
∀Rule r ∈ Rules: 
    ∃VerificationFunction V_r: Code → {Valid, Invalid}
    such that:
        V_r(c) = Valid ⟹ c satisfies r
        V_r(c) = Invalid ⟹ c violates r
        RuntimeOnly(V_r) = false
```

**Popperian Criterion:** A rule without static verification is unfalsifiable and therefore unscientific. The `human_factor_reliance: FORBIDDEN` clause enforces objectivity.

---

## 2. Invariant Theory

### 2.1 Architectural Flow Invariant

**Statement:** Execution flow is STRICT_UNIDIRECTIONAL.

**Mathematical Structure:**
```
Let G = (N, E) be the execution graph where:
    N = {Payload, Receiver, Pipeline, StateMutation, EDAEvent, EventHandler}
    E = {(n₁, n₂) | n₁ precedes n₂ in execution}

Invariant: G is a Directed Acyclic Graph (DAG)
    ∀cycles C in G: |C| = 0
    
This ensures: No temporal paradoxes, no infinite loops, deterministic termination.
```

**Proof of Termination:**
Since G is a finite DAG with |N| = 6 nodes, any path has maximum length 5. Therefore, all executions terminate in O(1) steps relative to the architectural topology.

### 2.2 CQRS Separation Invariant (ARM005)

**Statement:** No component may contain both Read (Query) and Write (Command) operations.

**Set-Theoretic Formalization:**
```
Let C be the set of all components.
Let R(c) = {read operations in c}
Let W(c) = {write operations in c}

Invariant: ∀c ∈ C: R(c) ≠ ∅ ⟹ W(c) = ∅  (Query)
         ∀c ∈ C: W(c) ≠ ∅ ⟹ R(c) = ∅  (Command)
         
Equivalently: R(c) ∩ W(c) = ∅ for all side-effectful operations
```

**Category-Theoretic Interpretation:**
The system forms a category **Armatura** where:
- Objects: System states (State₁, State₂, ...)
- Morphisms: Commands (mutations) and Queries (projections)
- **Invariant:** No morphism is both epic (Command) and monic (Query) simultaneously in the same component.

This separation ensures the system maintains the **Law of Demeter** at the architectural level.

---

### 2.3 Component Scope Invariant

**Statement:** Rules apply selectively based on component position in architectural hierarchy.

**Formalization:**
```
Let H = {Application, Plugin, ExternalAbstraction, System} be the component hierarchy.
Let Scope: Rules → P(H) define valid levels for each rule.

For any rule r and component c:
    Apply(r, c) ⟺ Level(c) ∈ Scope(r)

Example mappings:
    Scope(ARM008) = {Plugin}                    // Pipeline nodes only
    Scope(ARM010) = {Plugin}                    // Pipeline resource management
    Scope(ARM007) = {Plugin, ExternalAbstraction} // Inheritance requirements
```

**Justification (Pragmatic Scope Restriction):**
Armatura rules target specific architectural concerns. External abstractions (IGameProvider) implement external API bridges, not pipeline execution. System components (HttpClient) are managed by runtime. Applying pipeline rules to non-pipeline components creates false positives without improving correctness.

**Proof of Non-Interference:**
```
∀c: Level(c) = ExternalAbstraction ⟹ c ∉ {IQueryPlugin, ICommandPlugin}
∴ ARM008(c) is undefined (no free-form async concern)

∴ Scope restriction preserves rule validity.
```

---

## 3. Compositional Algebra

### 3.1 Rule Composition Laws

**Statement:** Rules compose associatively without emergent conflicts.

**Algebraic Structure:**
```
Let (Rules, ∘) be a monoid where:
    - r₁ ∘ r₂ = sequential application of r₁ then r₂
    - Identity: ε = "no constraint"
    
Associativity: (r₁ ∘ r₂) ∘ r₃ = r₁ ∘ (r₂ ∘ r₃)

Theorem: If all rules are locally consistent, their global composition is consistent.

Proof Sketch:
Each rule rᵢ defines a constraint set Cᵢ ⊆ CodeSpace.
The global constraint: C_global = ⋂ᵢ Cᵢ
If ∀i: Cᵢ ≠ ∅ (each rule satisfiable), then C_global ≠ ∅ (composition satisfiable).

This follows from the Finite Intersection Property in compact code spaces.
```

### 3.2 The Nomadic Invariant

**Statement:** System behavior is invariant under host migration.

**Formalization:**
```
Let H = {h₁, h₂, ...} be the set of valid hosts.
Let B(h) be the behavior function on host h.

Nomadic Invariant: ∀h₁, h₂ ∈ H: B(h₁) ≅ B(h₂)
    where ≅ denotes behavioral equivalence (observationally indistinguishable)
    
Corollary: No absolute paths, no host-specific crypto (DPAPI), no registry dependencies.
```

**Group-Theoretic Interpretation:**
The system exhibits symmetry under the **Host Transformation Group** Φ:
```
∀φ ∈ Φ: φ(System) ≅ System
```
This is the architectural equivalent of **Lorentz invariance** in physics—physical laws are the same in all inertial frames.

---

## 4. Popperian Falsifiability

### 4.1 Empirical Content of Rules

**Statement:** Each rule must have empirical content (potential falsifiers).

**Formalization:**
```
For each rule r, define:
    - F_r = {code snippets that would violate r}
    - E_r = {empirical tests that can detect violations}
    
Falsifiability Criterion: |F_r| > 0 and |E_r| > 0

Examples:
    r = "FORBIDDEN: runtime DI containers"
    F_r = {code using ServiceCollection.BuildServiceProvider()}
    E_r = {Roslyn analyzer detecting ServiceProvider usage}
```

### 4.2 The Verifiability Spectrum

```
FORBIDDEN rules: High falsifiability (easy to detect violation)
MANDATORY rules: High falsifiability (easy to detect absence)
MUST rules: Maximum falsifiability (absolute compliance checkable)
```

**Degenerate Case:**
A rule like "code should be good" is unfalsifiable—no concrete F_r exists. Such rules are excluded from Armatura by the `measurability` axiom.

---

### 4.3 Resource Classification Theorem

**Statement:** Resource management rules apply by resource lifecycle category.

**Formalization:**
```
Let R be the set of all disposable resources.
Define Category: R → {HostManaged, RuntimeManaged, UserManaged}

Where:
    HostManaged    = {FileStream, SqlConnection, custom IDisposable}
    RuntimeManaged = {HttpClient, SemaphoreSlim, CancellationTokenSource}
    UserManaged    = {UI components, temporary caches}

ARM010 applies to:
    {r ∈ R | Category(r) = HostManaged}

With falsifier set:
    F_r = {using FileStream, new FileStream(...), stream.Dispose()}
    E_r = {Roslyn analyzer detecting FileStream instantiation}
```

**Justification (Category-Theoretic Classification):**
Different resources have different ownership semantics. RuntimeManaged resources have deterministic finalization via CLR. HostManaged resources require explicit lifecycle coordination. UserManaged resources fall outside compile-time verification scope.

This classification maintains decidability: we can statically distinguish FileStream (compile-time type) from HttpClient (also compile-time type) by their Category mapping.

**Empirical Basis:**
```
Observation: HttpClient disposal timing doesn't affect pipeline determinism.
Observation: FileStream disposal timing affects file locking and availability.
∴ Different treatment is empirically justified.
```

---

## 5. Causal Structure

### 5.1 Causal Graph of Architectural Decisions

**Statement:** Every rule has traceable causal ancestry.

**Causal Model:**
```
Let DAG G_causal = (Decisions, CausalLinks)

Example paths:
    "Spaghetti code crisis" → "FORBIDDEN: free-form async Task"
                              → "MANDATORY: QuantizedNode inheritance"
                              
    "Memory leak in production" → "FORBIDDEN: plugin-side resource cleanup"
                                  → "MANDATORY: Host-managed DAG Ref Counting"
```

**Counterfactual Analysis:**
For each rule r, we can ask: "What would happen if ¬r?"
    
This validates r through **potential outcomes** (Rubin causal model).

### 5.2 The Do-Calculus of Architectural Intervention

**Statement:** Rules support intervention analysis.

```
P(Behavior | do(EnforceRule(r))) vs P(Behavior | do(¬EnforceRule(r)))

The causal effect: ACE = E[Quality | do(r)] - E[Quality | do(¬r)]

A rule is causally justified if ACE > ε (positive causal effect).
```

---

## 6. Ergodic Theory

### 6.1 Time Averages vs. Ensemble Averages

**Statement:** System behavior is statistically predictable across time and instances.

**Ergodic Hypothesis:**
```
For metric M (e.g., "build success rate"):
    
    lim_{T→∞} (1/T) ∫₀ᵀ M(System(t)) dt  =  E[M(System)]
    
    Time average          Ensemble average
    
Consequence: A single long-running Armatura-compliant system
exhibits the same statistical properties as an ensemble of such systems.
```

### 6.2 Mixing and Decay of Correlations

**Statement:** Violations of one rule don't correlate with violations of others (independence).

**Mixing Property:**
```
Let A = "violation of r₁", B = "violation of r₂"

Independence: P(A ∩ B) = P(A) · P(B)

This ensures: Detection of one violation doesn't predict others.
              Each rule can be tested in isolation.
```

---

## 7. Occam's Razor Formalized

### 7.1 Minimum Description Length

**Statement:** Among all valid architectural constitutions, Armatura minimizes description length.

**MDL Criterion:**
```
Let H = set of all possible architectural constitutions
Let L(h) = Kolmogorov complexity of constitution h

Armatura = argmin_{h ∈ ValidConstitutions} L(h)

Subject to:
    ∀h: Valid(h) ⟹ System(h) satisfies requirements
```

**Evidence:**
- FORBIDDEN/MANDATORY binary classification (minimal alphabet)
- No redundant rules (verified by pairwise independence)
- Compact mathematical formulas instead of prose

### 7.2 Rule Redundancy Check

**Algorithm:**
```python
def is_redundant(r_new, existing_rules):
    """
    r_new is redundant if:
        ∀code: Violates(r_new, code) → ∃r ∈ existing_rules: Violates(r, code)
    """
    for code in all_possible_code:
        if violates(r_new, code):
            if not any(violates(r, code) for r in existing_rules):
                return False  # Not redundant—covers new case
    return True  # Redundant—all cases already covered
```

---

## 8. Mathematical Dependencies

### 8.1 Routing Specificity Formula

```
Specificity = Σ(ExactMatch × 1 + WildcardMatch × 0)

Proof of Unambiguity:
    If two routes have equal specificity, the collision resolution
    falls back to integer priority values, ensuring total ordering.
```

### 8.2 Striped Path Locking (Modular Arithmetic)

```
SegmentIndex = Hash(Path) mod 64

Properties:
    - Deterministic: Same path → Same index
    - Uniform: Well-distributed across 64 slots
    - Isolation: Different paths rarely collide
```

### 8.3 Continuous Proportional Backoff

```
Action(Utilization) = {
    [0, 0.5):     Normal execution
    [0.5, 0.75):  Bundle aggregation (batching)
    [0.75, 0.9):  Micro-quantum yield (throttling)
    [0.9, 1.0]:   Emergency evacuation (shedding)
}

This forms a piecewise linear Lyapunov function ensuring
system stability under load.
```

---

## 9. Metatheoretical Properties

### 9.1 Consistency

**Theorem:** Armatura is consistent (no contradictions).

**Proof:**
```
All FORBIDDEN/MANDATORY pairs are disjoint:
    ∀r: ¬(FORBIDDEN(r) ∧ MANDATORY(r))
    
All rules are satisfiable simultaneously in the reference implementation.
∴ ∃Model ⊨ Armatura
∴ Armatura is consistent.
```

### 9.2 Completeness (within scope)

**Theorem:** Armatura is complete for its domain.

**Definition:**
```
For any architectural decision d in {DI, CQRS, state management, ...}:
    either: d is covered by some rule r ∈ Armatura
    or:    d is explicitly excluded from scope (e.g., "UI layer exempt")
```

### 9.3 Independence from Implementation

**Theorem:** Armatura specifies WHAT, not HOW.

**Evidence:**
- No specific class names (except examples)
- No framework versions
- Abstract constraints applicable to any language/runtime

---

### 9.4 Legacy Compatibility Theorem

**Statement:** Temporary rule exemptions are valid with explicit markers, deadlines, and causal justification.

**Formalization:**
```
Define Exemption(e, r, c) as a temporary non-application of rule r to component c.

ValidExemption(e) ⟺
    ∃ Marker(m): m ∈ {TODO, FIXME, pragma warning disable} ∧ 
    ∃ Deadline(d): d ∈ ISO8601 ∧ d > Now() ∧
    ∃ Justification(j): CausalLink(j, r) ∧
    ∃ Owner(o): o ∈ TeamMembers

Temporal enforcement:
    Severity(e) = Warning  if Now() < d - 30 days
    Severity(e) = Error    if Now() ≥ d

Example:
    // TODO: Refactor to host-managed resource (deadline: 2026-12-01)
    #pragma warning disable ARM010
    using FileStream stream = ...
    #pragma warning restore ARM010
```

**Justification (Popperian Degeneration Protection):**
Without explicit markers, exemptions become unfalsifiable (we can't track them). Without deadlines (ISO8601 format per Axiom 1.2), they become permanent (violating Occam's Razor—unnecessary entities). Without causal justification, they become ad-hoc (non-scientific).

This theorem preserves falsifiability: exemptions are observable, time-bounded, and traceable to specific architectural constraints.

**Proof of Consistency:**
```
Assume ∃ permanent exemption e with no deadline.
Then: ¬∃ date when r applies to c
∴ r is not falsifiable for c
∴ violates Popperian Criterion (Section 1.2)
∴ ValidExemption(e) requires finite deadline

∴ Legacy Compatibility preserves scientific status of Armatura.
```

---

## 10. Philosophical Synthesis

### 10.1 Epistemological Status

Armatura is a **normative scientific theory**:
- **Descriptive:** It describes patterns that work (empirical generalization)
- **Prescriptive:** It mandates patterns that should be used (normative force)
- **Falsifiable:** Each rule can be empirically tested (Popperian)

### 10.2 Ontological Commitments

Armatura commits to the existence of:
1. **Components** (reified as classes/modules)
2. **Flows** (reified as method calls/events)
3. **States** (reified as data structures)
4. **Invariants** (reified as compile-time checks)

These are **pragmatic posits**—not metaphysical necessities, but useful fictions that enable prediction and control.

### 10.3 Unity of Principles

All principles in Armatura derive from four meta-principles:

1. **Explicitness over Implicitness** (convention→configuration, magic→manifest)
2. **Unidirectionality over Cyclicity** (DAGs→graphs, strict→loose coupling)
3. **Compositionality over Monolithicity** (plugins→monoliths, quantized→free)
4. **Verifiability over Trust** (static→runtime, automated→manual)

---

## Appendices

### A. Gödelian Limitations

**Theorem:** Armatura cannot prove its own consistency (by Gödel's Second Incompleteness Theorem).

**Response:**
Consistency is established by:
1. External verification (reference implementation)
2. Model existence (working system)
3. Incremental validation (each rule tested independently)

### B. Future Extensions

Potential theoretical expansions:
- **Probabilistic rules** ("MUST with probability p")
- **Temporal logic** ("MUST until condition")
- **Modal logic** ("MUST in all possible worlds")

These remain outside current scope to maintain decidability.

---

## 11. Meta-Invariance Axiom (Temporal Invariance)

### 11.1 Statement

**Statement:** Valid code remains valid under context evolution without modification.

**Formalization:**
```
Let C = {c₁, c₂, ...} be the set of code artifacts
Let T = {t₀, t₁, ...} be discrete time points
Let Context(t) be the system context at time t
Let Valid(c, context) ⟺ code c is correct in given context

Meta-Invariance: ∀c ∈ C, ∀tᵢ, tⱼ ∈ T:
    Valid(c, Context(tᵢ)) ⟹ Valid(c, Context(tⱼ))
    where Context(tⱼ) = Evolution(Context(tᵢ), Δrules)
    
Corollary: Validity is preserved under rule evolution
```

### 11.2 Explicit Configuration Invariant

**Statement:** Context-dependency MUST be externalized to manifests, not embedded in code.

**Formalization:**
```
Let H(c) = {hardcoded values in c}
Let M = manifest state (external configuration)

Meta-Invariant Code: ∀c: H(c) = ∅ ∨ H(c) ⊆ {universal constants}

∀contextual_value v: 
    ¬(v ∈ H(c)) ⟹ v ∈ M
    where M is independently versioned, validated, and migrated
```

**Justification (Occam's Razor):**
Hardcoding duplicates context state into code. Externalizing eliminates this redundancy, achieving minimum description length across the code+manifest system.

**Justification (SRP):**
Code contains logic. Manifests contain context. Separation ensures single reason for change: logic changes for new requirements, manifest changes for new environments.

### 11.3 Temporal Falsifiability

**Statement:** Code validity MUST be verifiable without execution in target context.

**Formalization:**
```
∀c: Valid(c, Context(tⱼ)) ⟺ ∃V: V(c, M(tⱼ)) = Valid
where V is static verification function
      M(tⱼ) is manifest at time tⱼ

Note: V(c, M(tⱼ)) = Valid ⟹ Execution(c, Context(tⱼ)) = Success
      with high probability (not guaranteed due to runtime factors)
```

**Verification Hierarchy:**
1. **Static Analysis:** Type checking, linting, SRP enforcement
2. **Manifest Validation:** Variable resolution, path interpolation
3. **Contract Verification:** Interface conformance, null checks
4. **Integration Testing:** Component interaction (requires execution)

### 11.4 Context Evolution Protocol

**Statement:** Context changes MUST be explicit, versioned, and reversible.

**Formalization:**
```
Evolution(Context(tᵢ), Δ) = Context(tᵢ₊₁)

Requirements:
1. Explicit: Δ is declared, not emergent
2. Versioned: ∃version(Context(tᵢ₊₁)) > version(Context(tᵢ))
3. Reversible: ∃Δ⁻¹: Evolution(Context(tᵢ₊₁), Δ⁻¹) = Context(tᵢ)
4. Validated: Valid(c, Context(tᵢ₊₁)) is checked before deployment
```

**Corollary:** No "magic" configuration changes. All context evolution is tracked, reviewed, and tested.

### 11.5 Agentic Execution Invariant

**Statement:** Code MUST be executable by autonomous agents without human interpretation.

**Formalization:**
```
Let A = autonomous agent (AI, CI/CD, automated tool)
Let Interpretable(code, agent) ⟺ agent can execute code correctly

Meta-Invariant Code: ∀c, ∀A ∈ ValidAgents: Interpretable(c, A)

Requirements for Interpretable:
1. No implicit conventions (configuration is explicit)
2. No undocumented assumptions (all preconditions in manifest)
3. No human-in-the-loop decisions (deterministic branching)
4. No environment-dependent implicit state (all state explicit)
```

**Justification (Composability):**
Human-dependent code cannot be composed automatically. Agentic code enables: automated testing, CI/CD integration, autonomous deployment, AI-assisted development.

**Example (Agent-Executable):**
```csharp
// Explicit configuration from manifest
string mcDir = variables["mcDir"];
string version = variables["gameVersion"];
var forgeVersion = ForgeVersionParser.Parse(version);
// No ambiguity: parser behavior is deterministic and documented
```

**Example (Non-Agentic):**
```csharp
// Implicit assumption, hardcoded logic
if (version.Contains("forge")) { /* guess format */ }
// Ambiguity: what if format changes? Agent cannot know intent.
```

---

## Conclusion

Armatura represents a **scientific architectural constitution** grounded in:
- **Mathematical rigor** (formal invariants, compositional algebra)
- **Scientific methodology** (Popperian falsifiability, empirical testability)
- **Philosophical coherence** (Occam's razor, causality, ergodicity)
- **Meta-invariance** (temporal stability, agentic executability)

This theory provides the epistemological foundation for why Armatura rules exist, why they have their specific forms, and how they collectively ensure system correctness, maintainability, and nomadic portability.

**The theory itself is falsifiable:** If any rule in Armatura is shown to be:
1. Unverifiable (no static check possible), or
2. Unjustified (no causal benefit), or
3. Redundant (covered by other rules), or
4. Inconsistent (contradicts other rules)

...then the theory requires revision, per its own principles.

---

*Document version: 1.1*
*Last updated: 2026-05-31*
*Status: Formalized Invariant Theory with Meta-Invariance Axiom*
