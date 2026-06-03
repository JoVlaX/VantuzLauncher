# Theory Gap Retrospective: Why the Theoretical Framework Systematically Permits Recurrence

**Audit Date:** 2026-06-03
**Auditor:** Cascade Agent
**Method:** Third-order retrospective analyzing why INVARIANT_THEORY.md and COMPOSITUM_SPECIFICATION.md failed to prevent both code violations and meta-violations across sessions.

---

## 1. Executive Summary

Five cognitive patterns (H1–H5) caused recurrence of violations. This analysis identifies the **structural missing axiom** that makes those patterns inevitable: **the Reflexivity Gap**. INVARIANT_THEORY.md applies all its criteria *outward* (to code) but contains no principle requiring *inward* application (to plans, audits, retrospectives). COMPOSITUM_SPECIFICATION.md operationalizes the theory for the codebase but never operationalizes it for the process of building the codebase.

**The single missing principle:** *Any document asserting compliance with Armatura must itself be demonstrably compliant with the same criteria.*

Without this principle:
- Retrospectives can demand falsifiability from code while remaining unfalsifiable themselves (H1, H2)
- Deviation deadlines can be enforced on code exemptions but not on improvement actions (H5)
- Loader completeness can be stated as a goal (`V = ∧V_i`) but never checked as a deliverable (H3)
- Counterfactual claims about agent knowledge can pass as "root cause analysis" (H4)

---

## 2. The Reflexivity Gap

### 2.1 Definition

A theoretical framework has a **Reflexivity Gap** when it specifies criteria for its objects of analysis but does not specify criteria for its own artifacts.

### 2.2 Evidence from INVARIANT_THEORY.md

**§1.2 Axiom of Measurability** states:
> "All rules MUST be enforceable by build-time tooling, tests, or deterministic static validation."
> `∀Rule r ∈ Rules: ∃VerificationFunction V_r: Code → {Valid, Invalid}`

**The gap:** The domain of `V_r` is `Code`. The axiom does not extend to:
- Plans (`plans/*.md`)
- Audits (`docs/audits/*.md`)
- Retrospectives (`retrospective-*.md`)
- Deviation protocols (`docs/deviations/*.md`)

**Result:** A retrospective can claim "§1.2 requires build-time verification" while itself lacking any build-time verification. The axiom is applied outward to the codebase but not inward to the analysis artifact.

**§4.1 Falsifiability Principle** defines:
> `F_r = {code snippets that would violate r}`
> `E_r = {empirical tests that can detect violations}`

**The gap:** `F_r` and `E_r` are defined for *code rules*. There is no `F_doc` or `E_doc` for *document claims*. When the retrospective states "pre-session read would have revealed..." (U1), this claim has no `F_r` (no file system state can prove or disprove an agent's counterfactual knowledge) and no `E_r` (no test can detect it). The falsifiability principle does not reflexively apply to the documents that invoke it.

**§9.4 Legacy Compatibility Theorem** requires:
> `∃Deadline(d): d ∈ ISO8601 ∧ d > Now()`

**The gap:** The theorem applies to `Exemption(e, r, c)` — temporary rule exemptions in code. It does not apply to:
- Preventive mechanisms in retrospectives
- Improvement actions in plans
- Self-correction protocols

**Result:** DEVIATION-002 Phase 4 lacks a deadline → flagged as HIGH violation. The retrospective's own 5 preventive mechanisms lack deadlines → invisible. The theorem's scope is explicitly exemptions, not improvements, creating the asymmetric temporal valuation (H5).

### 2.3 Evidence from COMPOSITUM_SPECIFICATION.md

**§0.2 Update Protocol** defines cascading verification:
> `Changes to Level 1 documents trigger mandatory verification cascade to Level 2.`
> `Affected = {COMPOSITUM_SPECIFICATION.md}`

**The gap:** The cascade stops at Level 2. There is no cascade to:
- Level 3: `COMPOSITUM.md` (AI agent onboarding manifest)
- Level 4: Category-specific documentation
- **Level 0: Plans and retrospectives** (which exist outside the document hierarchy entirely)

The plan `retrospective-root-causes-622aab.md` is not a Level 1–4 document. It is an agent-generated artifact with no parent, no version, and no verification cascade target. When the theory changes (e.g., §1.2 is reinterpreted to include document verification), there is no mechanism to propagate that change to plans.

**§4.2 Compositum Core** defines:
> `Loader: Assembly → (I: Valid/Invalid)`

**The gap:** The formalization specifies `Assembly → (I: Valid/Invalid)` but does not define:
- What `I` explicitly includes (name, CQRS, resource, scope)
- How to verify that the Loader itself is complete
- A checkable artifact proving `Loader` implements the full function

**Result:** `PluginNameVerifier.cs` implements `Assembly → Name`, which is a partial Loader. The specification does not provide a completeness criterion that would have caught this at implementation time. §4.2 is declarative, not operational.

**§7.1 Mandatory Verification** states:
> `V = conjunction of all verifiers from INVARIANT_THEORY`
> `∀code ∈ Compositum: ∃V: Code → {Valid, Invalid}`

**The gap:** "conjunction of all verifiers" is a mathematical ideal, not an operational checklist. There is no:
- `V_completeness` function that checks whether all required verifiers exist
- "V completeness dashboard" listing implemented vs. missing verifiers
- Build-time check that `|V_implemented| = |V_required|`

**Result:** Only `Name` verifier exists; CQRS, DAG, resource, scope verifiers are missing. But §7.1 gives no mechanism to detect this incompleteness. The formula `V = ∧V_i` is satisfied vacuously if `V` is just `{Name}` — there is no enumeration of required `V_i`.

**§7.3 Continuous Verification** defines:
> `V_build includes: Roslyn analyzers, Architectural boundary tests, Composition validity checks`

**The gap:** `V_build` includes code verification only. It does not include:
- Plan verification before execution
- Retrospective claim verification before acceptance
- Deviation protocol structural validation (deadline presence, status consistency)

**Result:** Plans can be created, executed, and archived without ever being validated against the same criteria they apply to code.

**§8.2 Meta-Invariance** states:
> `∀code ∈ Compositum, ∀tᵢ, tⱼ: Valid(code, Context(tᵢ)) → Valid(code, Context(tⱼ))`

**The gap:** "Meta-Invariance" preserves code validity across time. It does not preserve *document validity* across analysis levels. A code audit (Level 2 analysis) is not required to remain valid when the same criteria are applied to the audit document itself. The theorem is temporal, not reflexive.

---

## 3. Mapping Cognitive Patterns to Theory Gaps

| Pattern | Root Cause | Missing Theoretical Principle | Document Location |
|---------|-----------|----------------------------|-------------------|
| **H1** Inspector's Dilemma | Self-analysis excluded from scope | No Self-Application Axiom: documents asserting compliance must be verifiable | INVARIANT_THEORY §1.2 (domain = Code only) |
| **H2** Recursive Completeness Illusion | Meta-analysis feels self-sufficient | No Reflexive Closure: analysis artifacts must pass the same checks as analyzed artifacts | INVARIANT_THEORY §4.1 (F_r/E_r for code only) |
| **H3** Competence Curse | Complex analysis crowds out trivial fixes | `V = ∧V_i` is declarative; no operational completeness check | COMPOSITUM_SPEC §7.1 (no V_completeness function) |
| **H4** Context Blindness | Agent-state counterfactuals treated as facts | No Document Falsifiability: claims in plans/audits must have F_r and E_r | INVARIANT_THEORY §4.1 (F_r/E_r not extended to documents) |
| **H5** Deadline Blindness | Deadlines only for exemptions, not improvements | Asymmetric Temporal Valuation: §9.4 applies only to exemptions | INVARIANT_THEORY §9.4 (scope = Exemptions only) |

---

## 4. Minimal Axiom Proposals

### Proposal A: §1.2a Reflexive Measurability (INVARIANT_THEORY.md)

**Add after §1.2:**

```markdown
### 1.2a Corollary: Reflexive Measurability

**Statement:** Any document, plan, or artifact asserting compliance with Armatura MUST itself be statically verifiable against the same criteria.

**Formalization:**
```
∀Artifact a ∈ Assertions:
    AssertsCompliance(a, Armatura) →
    ∃V_a: Artifact → {Valid, Invalid}:
        V_a(a) = Valid ⟹ a satisfies Armatura
```

**Scope:** Includes plans, audits, retrospectives, deviation protocols, and agent-generated analysis.

**Consequence:** A retrospective claiming "§1.2 requires build-time verification" must itself contain a verifiable checklist or automated test. A plan proposing preventive mechanisms must include ISO8601 deadlines (per §9.4 applied symmetrically).
```

### Proposal B: §4.1a Document Falsifiability (INVARIANT_THEORY.md)

**Add after §4.1:**

```markdown
### 4.1a Corollary: Document Falsifiability

**Statement:** Every claim in an Armatura-compliant document must have concrete falsifier set `F_doc` and empirical test `E_doc`.

**Formalization:**
```
For each claim c in document d:
    F_doc(c) = {file system states that would falsify c}
    E_doc(c) = {automated check or manual inspection detecting falsification}
    
    ValidClaim(c) ⟺ |F_doc(c)| > 0 ∧ |E_doc(c)| > 0
```

**Agent-state claims:** Claims depending on the generator's internal state ("would have", "could have", "should have") MUST be marked [HYPOTHESIS] and excluded from `ValidClaim` unless accompanied by an observable proxy (e.g., session scope document, commit history).
```

### Proposal C: §9.4a Symmetric Temporal Enforcement (INVARIANT_THEORY.md)

**Add after §9.4:**

```markdown
### 9.4a Corollary: Symmetric Deadlines

**Statement:** Any time-bounded action — whether exemption (negative) or improvement (positive) — MUST have an ISO8601 deadline.

**Formalization:**
```
Define Action(a, type, deadline):
    type ∈ {Exemption, Improvement, Remediation, Prevention}
    
ValidAction(a) ⟺ ∃Deadline(d): d ∈ ISO8601 ∧ d > Now()
```

**Consequence:** Retrospective preventive mechanisms, roadmap items, and self-correction protocols require deadlines with the same rigor as deviation protocols.
```

### Proposal D: §0.3 Plan Verification Protocol (COMPOSITUM_SPECIFICATION.md)

**Add after §0.2:**

```markdown
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
```

### Proposal E: §7.1a V Completeness Dashboard (COMPOSITUM_SPECIFICATION.md)

**Amend §7.1:**

Add to the formalization:
```
V_complete ⟺ |V_implemented| = |V_required| ∧ ∀v ∈ V_required: v ∈ V_implemented

V_required = {
    NameVerifier, CQRSVerifier, ResourceVerifier,
    ScopeVerifier, DAGVerifier, NomadicVerifier, ...
}
```

Add requirement:
> Every build MUST output a `V_completeness_report.json` listing implemented vs. required verifiers. A missing verifier is a build warning; a missing verifier without deviation protocol is a build error.

---

## 5. Conclusion

The recurrence of violations was not caused by agent negligence or random error. It was caused by a **predictable structural property of the theoretical framework**: the Reflexivity Gap. INVARIANT_THEORY.md and COMPOSITUM_SPECIFICATION.md are designed as *descriptive/prescriptive* documents about code, not as *self-governing* documents about the process of building code.

**The 5 minimal patches above would close the gap without overcomplicating the framework:**
- §1.2a ensures plans and audits are verifiable, not just code
- §4.1a eliminates unfalsifiable claims from all artifacts
- §9.4a removes the exemption/improvement deadline asymmetry
- §0.3 creates an explicit validation gate for plans
- §7.1a operationalizes the declarative `V = ∧V_i` into a checkable report

**With these patches:**
- H1 (Inspector's Dilemma) → impossible: self-analysis is mandatory per §1.2a
- H2 (Recursive Completeness) → impossible: `## Meta-Compliance` is required per §0.3
- H3 (Competence Curse) → detectable: `V_completeness_report` shows missing verifiers
- H4 (Context Blindness) → blocked: [HYPOTHESIS] flagging is mandatory per §4.1a
- H5 (Deadline Blindness) → blocked: all actions require deadlines per §9.4a

The theory is sound. Its **scope boundary** is the only flaw — and it is a fixable flaw.

---

*Per INVARIANT_THEORY §4.1 (Falsifiability): This report is itself falsifiable. If any of the 5 proposed patches already exist in the documents (and were missed by this audit), the Reflexivity Gap hypothesis is falsified. If the patches are added and recurrence still occurs, the hypothesis is also falsified.*
