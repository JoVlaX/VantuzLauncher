# Armatura Document Protocol

## A Practical Invariant System for Documentation Hierarchy Management

---

## Abstract

This document establishes a practical protocol for managing hierarchical documentation systems across all Armatura projects. While `INVARIANT_THEORY.md` provides the epistemological and ontological foundations, this protocol provides executable instructions for document versioning, cascading updates, backward compatibility, and real-time validation. It operationalizes §11.4 (Context Evolution Protocol) and §11.5 (Agentic Execution Invariant) specifically for documentation hierarchies.

**Keywords:** documentation hierarchy, semantic versioning, cascade updates, backward compatibility, agentic validation, manifest-driven governance

**Hierarchy Level:** 1 (Universal Protocol)  
**Parent:** INVARIANT_THEORY.md  
**Siblings:** All Armatura project specifications

---

## 1. Foundational Axioms

### 1.1 Axiom of Document Determinism

**Statement:** Document states are deterministic functions of their content and manifest declarations.

**Formalization:**
```
Let Doc = {content, path, metadata}
Let Manifest = {version, hash, parent_ref}

Determinism: ∀Doc₁, Doc₂:
    Hash(Doc₁.content) = Hash(Doc₂.content) ⟹ Doc₁ ≡ Doc₂
    
State(Doc) = f(content, Manifest)
where f is deterministic and reproducible
```

**Justification (§1.1 Determinism INVARIANT_THEORY):** Documentation must be interpreted unambiguously by both human readers and autonomous agents. Non-deterministic states (e.g., "latest version" references) violate temperature=0 regime.

**Popperian Criterion:**
```
F_r = {document without explicit version reference}
E_r = {manifest validation: parent_version must be concrete ⟨X.Y.Z⟩}
```

### 1.2 Axiom of Hierarchy Measurability

**Statement:** Document hierarchy compliance MUST be statically verifiable.

**Formalization:**
```
Let Hierarchy = {L₁, L₂, L₃, L₄} where L₁ = Universal Theory, L₄ = Implementation

Measurability: ∀doc ∈ Hierarchy:
    ∃V_doc: (content, manifest) → {Valid, Invalid}
    such that:
        V_doc = Valid ⟹ doc satisfies hierarchy invariants
        V_doc = Invalid ⟹ doc violates at least one invariant
        RuntimeOnly(V_doc) = false
```

**Justification (§1.2 Measurability INVARIANT_THEORY):** A documentation rule without static verification is unfalsifiable and therefore unscientific. The `human_interpretation_reliance: FORBIDDEN` clause enforces objectivity.

---

## 2. Document Hierarchy Invariant

### 2.1 Four-Level Hierarchy

**Statement:** Armatura documentation follows a strict four-level dependency chain.

**Formalization:**
```
Hierarchy (source of truth order):
    Level 1: INVARIANT_THEORY.md, ARMATURA_DOCUMENT_PROTOCOL.md
        — Universal Armatura principles and documentation protocols
        
    Level 2: {PROJECT}_SPECIFICATION.md  
        — Project-specific invariant instantiation
        
    Level 3: {PROJECT}.md (root)
        — AI agent onboarding and project identity
        
    Level 4: Category-specific documentation
        — Implementation details for plugin categories

Dependency: Level(n) strictly subordinate to Level(n-1)
Invariance: L₁ content changes invalidate L₂, L₃, L₄ unless updated
```

**Justification (§10.3 Compositionality INVARIANT_THEORY):** Hierarchical decomposition achieves minimum description length across the documentation system.

**Popperian Criterion:**
```
F_r = {doc at Level(n) without parent reference to Level(n-1)}
E_r = {hierarchy validation: parent_version must reference existing version}
```

---

## 3. Document Versioning Invariant

### 3.1 Semantic Versioning Protocol

**Statement:** Document versions follow strict hierarchical semantic versioning with deterministic parent-child relationships.

**Formalization:**
```
Let Dₙ = Document at Level n
Let V(D) = Semantic version ⟨major.minor.patch⟩

Version Invariant:
    (1) Parent Major Change ⟹ Child Major++:
        If V(Dₙ₋₁).major increases, then V(Dₙ).major ≥ V(Dₙ₋₁).major
        
        Justification: Breaking changes in parent invalidate child compatibility
    
    (2) Parent Minor Change ⟹ Child Minor++:
        If V(Dₙ₋₁).minor increases (feature addition), then V(Dₙ).minor++
        
        Justification: New parent features may enable new child capabilities
    
    (3) Independent Changes ⟹ Minor++:
        If Dₙ changes without Dₙ₋₁ change, then V(Dₙ).minor++
        
        Justification: Local evolution tracked independently
    
    (4) Patches ⟹ Patch++:
        V(Dₙ).patch++ for typos, formatting, non-substantive changes
        
        Justification: Cosmetic changes don't affect semantics
    
    (5) Parent Reference Rule:
        Dₙ.parent_version must equal V(Dₙ₋₁) at all times
        
        Dependency: V(Dₙ) compatible with V(Dₙ₋₁) ⟺ V(Dₙ).major == V(Dₙ₋₁).major
```

**Justification (§11.4 Context Evolution Protocol INVARIANT_THEORY):** Versioning makes evolution explicit, reversible, and machine-verifiable. Required for temporal falsifiability.

**Popperian Criterion:**
```
F_r = {Dₙ.parent_version ≠ V(Dₙ₋₁) at edit time}
E_r = {agent pre-write verification of parent_version match}
```

---

## 4. Cascading Update Protocol

### 4.1 Cascade Trigger Mechanism

**Statement:** Changes to Level N-1 trigger mandatory verification and potential update cascade to Level N.

**Formalization:**
```
CascadingUpdate(Dₙ₋₁, Δ):
    Preconditions:
        1. Δ is explicit with CausalLink(Δ, issue_or_requirement)
        2. V(Dₙ₋₁)' = ApplyVersionBump(V(Dₙ₋₁), Δ) (per §3.1)
    
    Cascade:
        Let Affected = {Dₙ ∈ Children(Dₙ₋₁)}
        
        ∀Dₙ ∈ Affected:
            VerificationResult = VerifyCompatibility(Dₙ, Dₙ₋₁')
            
            Case Compliant:
                V(Dₙ)' = bump_minor(V(Dₙ))
                Update Dₙ.parent_version = V(Dₙ₋₁)'
                Mark: SYNC_COMPLETE
                
            Case Violation:
                Report: CASCADE_REQUIRED(Dₙ, Dₙ₋₁', violation_details)
                Options:
                    a) Fix Dₙ to restore compliance → proceed as above
                    b) File DeviationProtocol(Dₙ, violation, deadline, justification)
                    c) Block Dₙ₋₁' release until compliance restored
            
    Postconditions:
        ∀Dₙ ∈ Affected: 
            Dₙ.parent_version = V(Dₙ₋₁)' ∨ 
            DeviationProtocol active for Dₙ
```

**Agent Procedure (§11.5 Agentic Execution INVARIANT_THEORY):**
```
On editing Dₙ₋₁:
    1. Compute V(Dₙ₋₁)' per §3.1
    2. Identify all Dₙ with parent = Dₙ₋₁ (via manifest)
    3. Pre-edit: Report affected children count
    4. Post-edit: 
        a. Update Dₙ₋₁ in manifest
        b. Set cascade_tracking.pending_updates = Affected
        c. Require explicit confirmation for each Dₙ update or deviation filing
```

**Popperian Criterion:**
```
F_r = {Dₙ.parent_version outdated > 0 time units after Dₙ₋₁ update}
E_r = {agent report: "CASCADE_REQUIRED: Dₙ → update parent ref or file deviation"}
```

---

## 5. Backward Compatibility Theorem

### 5.1 Breaking Change Definition

**Statement:** Parent document breaking changes require explicit migration protocol for all affected child documents.

**Formalization (from §9.4 Legacy Compatibility INVARIANT_THEORY):**
```
Define BreakingChange(Dₙ₋₁, old_version, new_version):
    old_invariants = ExtractInvariants(Dₙ₋₁, old_version)
    new_invariants = ExtractInvariants(Dₙ₋₁, new_version)
    
    Affected = {Dₙ : Valid(Dₙ, old_invariants) ∧ ¬Valid(Dₙ, new_invariants)}
    
    If Affected ≠ ∅:
        BreakingChange(Dₙ₋₁) = true
        
        For each Dₙ ∈ Affected:
            Mark: Legacy(Dₙ, 
                deadline = Now() + T_migration,
                migration_path_required = true
            )
            
            Provide: MigrationPath(old_invariants, new_invariants, Dₙ)
                - Side-by-side invariant comparison
                - Automated transformation rules where applicable
                - Manual migration guide with examples
                
            Track: DeviationProtocol(
                marker = LEGACY_COMPATIBILITY,
                deadline = Now() + T_migration,
                justification = "Breaking change in parent Dₙ₋₁",
                owner = assigned_maintainer
            )

Temporal Escalation:
    Phase 1 (0-60% of T_migration):   Severity = Warning,  CI = pass with notice
    Phase 2 (60-100% of T_migration): Severity = Error,    CI = fail
    Phase 3 (>100% of T_migration):     Severity = Invalid,  block all releases
```

**Justification (§9.4 Popperian Degeneration Protection):** Without migration path, breaking changes become unfalsifiable barriers to adoption. Temporal escalation ensures deviations are transient, not permanent.

**Popperian Criterion:**
```
F_r = {breaking change without MigrationPath for all affected Dₙ}
E_r = {verification: ∀Dₙ ∈ Affected: MigrationPath(Dₙ) exists ∧ DeviationProtocol filed}
```

---

## 6. Detection & Verification Mechanism

### 6.1 Manifest-Driven Validation

**Statement:** Document compliance MUST be statically verifiable without execution or human interpretation.

**Formalization (§11.3 Temporal Falsifiability INVARIANT_THEORY):**
```
Let Manifest = {
    schema_version: "1.0",
    project: "ProjectName",
    documents: {
        "doc_id": {
            level: n ∈ {1,2,3,4},
            path: "relative/path.md",
            version: "X.Y.Z",
            hash: "sha256:hex_digest",
            hash_algorithm: "sha256",
            parent: "parent_doc_id | null",
            parent_version: "A.B.C | null",
            status: "active | deprecated | draft",
            last_modified: "ISO8601_timestamp"
        }
    },
    deviations: [
        {
            document: "doc_id",
            marker: "TODO | FIXME | DEVIATION",
            deadline: "ISO8601_timestamp",
            justification: "causal explanation",
            severity: "warning | error"
        }
    ],
    validation_rules: {
        enforce_parent_version: boolean,
        enforce_hash_match: boolean,
        enforce_required_sections: boolean,
        report_all_errors: boolean,
        no_grace_periods: boolean
    },
    cascade_tracking: {
        pending_updates: ["doc_id", ...],
        last_parent_change: "ISO8601_timestamp | null"
    }
}

VerifyHierarchy(Manifest):
    errors = []
    
    ∀(doc_id, doc) ∈ Manifest.documents:
        -- Verify parent reference
        If doc.parent ≠ null:
            parent = Manifest.documents[doc.parent]
            If doc.parent_version ≠ parent.version:
                errors.append(PARENT_VERSION_MISMATCH(doc_id, doc.parent_version, parent.version))
        
        -- Verify content hash
        current_hash = SHA256(read(doc.path))
        If doc.hash ≠ current_hash:
            errors.append(HASH_MISMATCH(doc_id, doc.hash, current_hash))
        
        -- Verify required sections (per level)
        required = GetRequiredSections(doc.level)  -- INVARIANT_THEORY.md defines for L1, L2
        content = read(doc.path)
        missing = required.filter(sect => ¬content.contains(sect))
        If missing ≠ ∅:
            errors.append(MISSING_SECTIONS(doc_id, missing))
    
    -- Check for outdated parent references after cascade
    ∀pending ∈ Manifest.cascade_tracking.pending_updates:
        parent = Manifest.documents[pending.parent]
        If pending.parent_version ≠ parent.version:
            errors.append(CASCADE_REQUIRED(pending, parent))
    
    Return {valid: errors == ∅, errors: errors}

Falsifiability:
    F_r = {Manifest.parent_version ≠ actual parent version}
    E_r = {Agent verification: f(Manifest) → (valid, errors)}
```

**Agentic Execution (§11.5):** Manifest is machine-readable. Verification function is deterministic: f(Manifest) → (valid, errors). No human interpretation required.

---

## 7. Real-Time Strict Validation Invariant

### 7.1 Zero-Tolerance Agent Enforcement

**Statement:** Every document change triggers immediate complete verification with zero tolerance for undeclared violations.

**Formalization:**
```
Let Edit = {target_path, new_content, timestamp, agent_id}
Let Verify(Manifest, Edit) → (valid, errors)

Validation(Edit):
    If Edit.target_path maps to HierarchyDocuments(Manifest):
        -- Pre-write checks
        (pre_valid, pre_errors) = VerifyContent(new_content, Edit.target_path)
        
        If ¬pre_valid:
            Return BLOCK(Edit, pre_errors, "Pre-write validation failed")
        
        -- Proceed with write (only if pre_valid)
        WriteFile(Edit.target_path, new_content)
        
        -- Post-write manifest update
        new_hash = SHA256(new_content)
        UpdateManifest(Edit.target_path, {hash: new_hash, last_modified: Edit.timestamp})
        
        -- Verify cascade implications
        doc = Manifest.documents[lookup(Edit.target_path)]
        children = GetChildren(doc, Manifest)
        
        If children ≠ ∅:
            Report: CASCADE_CHECK_REQUIRED(children, doc.version)
            -- Do NOT auto-update children; require explicit agent/user action
        
        Return SUCCESS
    
    Else (not a hierarchy document):
        Return STANDARD_WRITE  -- no special validation required

Requirements (§1.2 Determinism INVARIANT_THEORY):
    - No grace periods: valid is boolean
    - Complete visibility: all errors reported simultaneously (no early exit)
    - Immediate: at edit-time, not batched or deferred
    - Deterministic: same (Manifest, Edit) → same (valid, errors) always
```

**Agent Implementation Protocol:**
```
AgentEdit(document_path, new_content):
    1. Load Manifest
    2. If document_path ∉ HierarchyDocuments(Manifest):
        Proceed with standard write
        Return
    
    3. -- Pre-write validation
    doc = Manifest.documents[lookup(document_path)]
    
    -- Verify parent reference intact
    If doc.parent ≠ null:
        parent = Manifest.documents[doc.parent]
        If new_content.parent_version ≠ parent.version:
            Report: PARENT_VERSION_MISMATCH
            Enumerate: ALL violations in new_content
            Block: Edit operation
            Return BLOCKED
    
    -- Verify required sections present
    required = GetRequiredSections(doc.level)
    missing = required.filter(sect => ¬new_content.contains(sect))
    If missing ≠ ∅:
        Report: MISSING_REQUIRED_SECTIONS(missing)
        Block: Edit operation
        Return BLOCKED
    
    -- Verify no structural violations
    (struct_valid, struct_errors) = ValidateStructure(new_content, doc.level)
    If ¬struct_valid:
        Report: STRUCTURAL_VIOLATIONS(struct_errors)
        Block: Edit operation
        Return BLOCKED
    
    4. -- All checks passed
    Proceed with write
    new_hash = SHA256(new_content)
    UpdateManifest(document_path, {hash: new_hash})
    
    5. -- Cascade awareness
    children = GetChildren(doc, Manifest)
    If children ≠ ∅:
        Report: "CASCADE_REQUIRED: The following documents may need updates: {children}"
        -- Do NOT auto-modify children
    
    Return SUCCESS

DeviationProtocol (only bypass mechanism):
    If deviation exists in Manifest.deviations for document_path:
        deviation = Manifest.deviations[document_path]
        If Now() < deviation.deadline:
            Report: WARNING (deviation active, deadline: deviation.deadline)
            Proceed with caution
        Else:
            Report: ERROR (deviation expired, MUST resolve before edit)
            Block: Edit operation
```

**Popperian Criterion:**
```
F_r = {edit with invalid hierarchy that proceeds to write}
E_r = {agent verification reporting all errors immediately, blocking invalid states}
```

---

## 8. Meta-Invariance for Documentation (§11.1 Application)

### 8.1 Valid Documents Remain Valid

**Statement:** Valid documentation remains valid under context evolution without content modification.

**Formalization (from §11.1 Meta-Invariance Axiom INVARIANT_THEORY):**
```
Let D = {d₁, d₂, ...} be the set of all documents
Let T = {t₀, t₁, ...} be discrete time points
Let Context(t) be the documentation hierarchy state at time t
Let Valid(doc, context) ⟺ document satisfies all invariants in given context

Documentation Meta-Invariance: ∀doc ∈ D, ∀tᵢ, tⱼ ∈ T:
    Valid(doc, Context(tᵢ)) ⟹ Valid(doc, Context(tⱼ))
    where Context(tⱼ) = EvolveHierarchy(Context(tᵢ), Δrules)
    
Corollary: Validity is preserved under parent document evolution IF:
    - doc.parent_version references compatible parent version (per §3.1)
    - OR DeviationProtocol is active and within deadline (per §5.1)

Violation Condition:
    Valid(doc, Context(tᵢ)) ∧ ¬Valid(doc, Context(tⱼ)) ⟹
        doc.parent_version outdated ∧ ¬DeviationProtocol active
```

**Justification (§11.1):** Without this invariant, documentation becomes unstable—valid states randomly become invalid through external changes. This preserves epistemic confidence in the documentation system.

**Connection to §5.1 (Backward Compatibility):**
- §5.1 provides the MECHANISM for handling parent breaking changes
- §8.1 establishes the INVARIANT that valid docs should remain valid
- Together: If parent breaks, child validity is preserved through:
    a) Graceful degradation via DeviationProtocol (time-bounded)
    b) Explicit migration to new parent version (permanent fix)

**Popperian Criterion:**
```
F_r = {document marked invalid solely due to parent evolution, without child content change}
E_r = {hierarchy validation: detect parent_version mismatches as separate error class}
```

---

## 9. Agent Onboarding Protocol

### 9.1 Session Initialization Procedure

**Statement:** Autonomous agents MUST follow deterministic onboarding sequence when entering new chat sessions.

**Formalization:**
```
AgentOnboarding(project_root):
    1. Locate manifest: project_root/docs/manifest.json
       If ¬exists: Report ERROR (manifest required for all Armatura projects)
    
    2. Read and parse Manifest
       Extract: project name, hierarchy levels, active documents, deviations
    
    3. Read documents in strict order:
        a) Level 1: INVARIANT_THEORY.md — universal principles
        b) Level 1: ARMATURA_DOCUMENT_PROTOCOL.md — this document
        c) Level 2: {PROJECT}_SPECIFICATION.md — project invariants
        d) Level 3: {PROJECT}.md — project identity
        e) Level 4: Category docs — if relevant to current task
    
    4. Self-verification checklist:
        □ I understand the project identity and ontological nature
        □ I understand the theoretical foundation (INVARIANT_THEORY)
        □ I understand the document hierarchy and my position in it
        □ I understand the validation obligations (this document §7)
        □ I understand current active deviations (if any)
    
    5. Mark onboarding complete in .windsurf/agent-onboarded.md
    
    6. Proceed with task execution under documented constraints

Multi-Project Environment:
    If multiple Armatura projects in workspace:
        For each project with manifest.json:
            Execute AgentOnboarding(project_root)
        Maintain separate context for each project
        Cross-project changes require verification in BOTH contexts
```

**Justification (§11.5 Agentic Execution INVARIANT_THEORY):** Deterministic onboarding ensures agents operate with complete context, preventing decisions based on partial information or assumptions.

**Popperian Criterion:**
```
F_r = {agent making hierarchy changes without reading complete chain}
E_r = {onboarding checklist verification before any document edits}
```

---

## 10. Philosophical Synthesis

### 10.1 Relation to INVARIANT_THEORY.md

**ARMATURA_DOCUMENT_PROTOCOL.md** is the operationalization of INVARIANT_THEORY.md for documentation systems:

| INVARIANT_THEORY.md | This Document (Operationalization) |
|---------------------|-----------------------------------|
| §1.1 Determinism | §1.1 Document Determinism, §3.1 Versioning |
| §1.2 Measurability | §1.2 Hierarchy Measurability, §6.1 Manifest Validation |
| §9.4 Legacy Compatibility | §5.1 Breaking Change Protocol |
| §11.1 Meta-Invariance | §8.1 Valid Documents Remain Valid |
| §11.4 Context Evolution | §4.1 Cascade Protocol, §3.1 Versioning |
| §11.5 Agentic Execution | §7.1 Agent Implementation, §9.1 Onboarding |
| §10.4 Additivity | §11.3 Evolvable Elements (linear complexity) |
| §10.5 Systemness | §2.1 Four-Level Hierarchy (emergent order) |

### 10.2 Universal Applicability

This protocol applies to ALL Armatura projects:
- Compositum (current instantiation)
- Vantuz
- Helm (future)
- Any project using Armatura principles

Project-specific adaptations:
- Manifest.json contains project-specific document list
- Required sections per level may vary by project complexity
- T_migration (§5.1) may be tuned per project velocity

Core protocol (§1-§9) remains invariant across all projects.

---

## 11. Document Modification Invariants

### 11.1 Structural Invariants (Non-Modifiable)

**Statement:** The "Armatura document format" consists of immutable structural properties that define document identity.

**Formalization:**
```
Let StructuralInvariant(doc) = {
    section_structure: ∀s ∈ FormalSections(doc):
        s.has_statement ∧ s.has_formalization ∧ s.has_justification ∧ s.has_popperian,
        
    hierarchy_position: doc.level ∈ {1,2,3,4} ∧ doc.parent.level = doc.level - 1,
    
    version_format: doc.version matches ⟨major.minor.patch⟩ ∧ major,minor,patch ∈ ℕ
}

Invariant: ∀doc ∈ ArmaturaDocuments:
    StructuralInvariant(doc) = true
    
ModificationRule:
    Modify(doc, Δ) where Δ affects StructuralInvariant ⟹ REJECT
```

**Justification (§10.3 Compositionality INVARIANT_THEORY):** Structure enables minimum description length. Changing structural invariants destroys the compositional algebra and epistemological guarantees.

**Popperian Criterion:**
```
F_r = {document with 4-part structure violation OR wrong hierarchy level OR invalid version format}
E_r = {structural validator: pattern matching + manifest consistency check}
```

---

### 11.2 Content Invariants — Bedrock Axioms (Non-Modifiable)

**Statement:** Foundational axioms in INVARIANT_THEORY.md are immutable content that defines Armatura's epistemological stance.

**Formalization:**
```
Let Bedrock = {
    "§1.1 Determinism": "Temperature=0, MDL selection",
    "§1.2 Measurability": "Static verification requirement",
    "§11.1 Meta-Invariance": "Valid docs remain valid under evolution",
    "Popperian Requirement": "Every formal section has F_r and E_r"
}

ContentInvariant(doc):
    doc.level == 1 ⟹ 
        ∀(section_id, content) ∈ Bedrock:
            doc.sections[section_id].semantics = content.semantics
            
EvolutionRule:
    Extend(doc, new_axiom): allowed, version.minor++
    Clarify(doc, existing_axiom): allowed, version.patch++ (semantics preserved)
    Modify(doc, bedrock_axiom, new_semantics): FORBIDDEN (would change Armatura identity)
```

**Justification (§1.2 Measurability INVARIANT_THEORY):** Bedrock axioms are what make the theory scientific. Changing them makes it a different theory, not an evolution of Armatura.

**Popperian Criterion:**
```
F_r = {Level 1 document with modified bedrock axiom semantics}
E_r = {diff tool: compare §1.1, §1.2, §11.1 semantics across versions}
```

---

### 11.3 Evolvable Elements (Modifiable with Constraints)

**Statement:** Specific invariants, examples, and project instantiations CAN evolve under strict protocols.

**Formalization:**
```
EvolvableElements(doc) = {
    specific_invariants: {s ∈ doc.sections | s.id ∉ Bedrock ∧ s.level > 1},
    examples: {content ∈ doc | content.type == "illustrative"},
    project_specialization: {content ∈ doc | doc.level ≥ 2}
}

EvolutionProtocol(element, Δ):
    If element ∈ EvolvableElements(doc):
        Case AddNew:
            ValidateStructure(Δ) per §11.1
            version = bump_minor(doc.version)
            
        Case Clarify:
            VerifySemanticsPreserved(doc.element, doc.element + Δ)
            version = bump_patch(doc.version)
            
        Case Deprecate:
            MarkWithDeviationProtocol(element, deadline, migration_path)
            version = bump_minor(doc.version)
            
        Case Remove:
            Only if universally superseded
            version = bump_major(doc.version)  -- BREAKING CHANGE
            
    Else:
        REJECT("Element is invariant, not evolvable")
```

**Justification (§11.4 Context Evolution Protocol INVARIANT_THEORY):** Evolution is explicit, versioned, and reversible. This preserves falsifiability while enabling growth.

**Popperian Criterion:**
```
F_r = {evolution without version bump OR without validation}
E_r = {manifest check: version bumped appropriately + structure validated}
```

---

### 11.4 Agent Modification Protocol

**Statement:** AI agents MUST verify all invariants before any document modification.

**Formalization:**
```
AgentModificationProtocol(doc_path, proposed_Δ):
    -- Load context
    doc = Read(doc_path)
    manifest = Read("docs/manifest.json")
    
    -- Invariant verification (blocking)
    Verify(§11.1 Structural Invariant, doc + proposed_Δ)
    Verify(§11.2 Content Invariant, doc, proposed_Δ)  -- if doc.level == 1
    
    -- Compute impact
    new_version = ComputeVersion(doc.version, proposed_Δ)
    children = GetChildren(doc, manifest)
    cascade_required = (children ≠ ∅)
    
    -- Validation results
    If all_invariants_passed:
        Return {
            permitted: true,
            new_version: new_version,
            cascade_required: cascade_required,
            affected_children: children
        }
    Else:
        Return {
            permitted: false,
            errors: invariant_violations,
            blocking_reason: "Invariant violation"
        }

ExecuteModification(doc, Δ, result):
    Preconditions: result.permitted == true
    
    Write(doc.path, doc.content + Δ)
    UpdateManifest(doc, result.new_version)
    
    If result.cascade_required:
        Report: "CASCADE_REQUIRED for {result.affected_children}"
        -- Agent MUST NOT auto-modify children
        -- User confirmation required for each child
```

**Justification (§11.5 Agentic Execution INVARIANT_THEORY):** Deterministic verification ensures agents operate with complete context. Invariant checking prevents decisions based on partial information.

**Popperian Criterion:**
```
F_r = {modification that proceeds despite invariant violation}
E_r = {agent protocol: all modifications pre-validated before write}
```

---

### 11.5 Modification Decision Matrix

**Statement:** Explicit rules for what can and cannot be modified.

**Decision Matrix:**

| Element | Can Modify? | Version Impact | Requires Cascade | Check |
|---------|-------------|----------------|------------------|-------|
| 4-part structure | **NO** | N/A | N/A | §11.1 |
| Hierarchy level | **NO** | N/A | N/A | §11.1 |
| Version format | **NO** | N/A | N/A | §11.1 |
| §1.1 Determinism | **NO** | N/A | N/A | §11.2 |
| §1.2 Measurability | **NO** | N/A | N/A | §11.2 |
| §11.1 Meta-Invariance | **NO** | N/A | N/A | §11.2 |
| F_r/E_r requirement | **NO** | N/A | N/A | §11.2 |
| Add invariant §X.Y | **YES** | minor++ | If Level < 4 | §11.3 |
| Clarify invariant | **YES** | patch++ | No | §11.3 |
| Deprecate invariant | **YES** | minor++ | Yes | §11.3 |
| Remove invariant | **YES** | major++ | Yes | §11.3 |
| Examples | **YES** | none | No | §11.3 |
| Project-specific | **YES** | per Δ | If parent | §11.3 |

**Legend:**
- **NO**: Structural or bedrock invariant — modification REJECTED
- **YES**: Evolvable element — modification permitted with version discipline

**Justification (§10.3 Explicitness over Implicitness INVARIANT_THEORY):** Explicit modification rules prevent ambiguity. Agents and humans have deterministic decision criteria.

**Popperian Criterion:**
```
F_r = {modification that violates the Decision Matrix}
E_r = {agent enforcement: matrix lookup before any modification}
```

---

## 12. Concurrent Modification Protocol

### 12.1 AI-Only Editing Invariant

**Statement:** Document modifications MUST be performed exclusively by autonomous AI agents with deterministic validation. Human direct editing of theory documents is FORBIDDEN.

**Formalization:**
```
Let Edit = {author, timestamp, validation_result, content_delta, hash}

ValidEdit(Edit) ⟺
    Edit.author ∈ AI_Agents ∧
    Edit.author ∉ Humans ∧
    Edit.validation_result = PASSED ∧
    VerifyAgainstINVARIANT_THEORY(Edit) = true ∧
    VerifyAgainstDOCUMENT_PROTOCOL(Edit) = true

HumanEditRestriction:
    ∀h ∈ Humans: ¬∃Edit where Edit.author = h ∧ Edit.target ∈ TheoryDocuments
    
Exception (None for Level 1-3 documents):
    DeviationProtocol allowed only for:
    - Level 4 category implementation files
    - Non-formal sections (examples, comments)
    - With explicit marker: [HUMAN_EDIT_VIA_DEVIATION_PROTOCOL]
    - Requires post-edit AI validation
```

**Justification (§1.1 Determinism + §11.5 Agentic Execution INVARIANT_THEORY):**

Human editing introduces non-deterministic interpretation. Temperature=0 regime
requires AI-only modification to preserve unambiguous interpretation. Human
factor reliance is FORBIDDEN per §1.2 Measurability.

**Popperian Criterion:**
```
F_r = {document edit with human author OR missing AI validation}
E_r = {manifest audit: author field must be AI_Agent_ID with validation hash}
```

---

## 13. Rollback Protocol with Cascade

### 13.1 Reversible Evolution Invariant

**Statement:** Document rollback to previous version MUST preserve hierarchy integrity and trigger reverse cascade.

**Formalization:**
```
Rollback(doc, target_version):
    Preconditions:
        1. target_version < doc.current_version
        2. ∃doc.history[target_version] with valid hash
        3. VerifyRollbackInvariant(doc, target_version) = true
        
    RollbackInvariant Check:
        -- Bedrock content must not change direction
        If doc.level == 1:
            Bedrock(doc at target_version) = Bedrock(doc at current)
            
        -- Structure must be preserved
        Structure(doc at target_version) = Structure(doc at current)
        
        -- Rollback is FORBIDDEN if:
        -- - target_version has different bedrock semantics
        -- - target_version has different structural invariants
        
    Reverse Cascade:
        children = GetChildren(doc, manifest)
        
        ∀child ∈ children:
            If child.parent_version > doc.version at target_version:
                -- Child was updated FOR this version, must rollback too
                Rollback(child, CompatibleVersion(child, target_version))
                -- OR mark as DIVERGED if incompatible
                
    Postconditions:
        doc.version = target_version
        doc.hash = doc.history[target_version].hash
        doc.status = "rolled_back"
        CascadeStatus = propagated OR diverged
```

**Justification (§11.4 Context Evolution + §11.1 Meta-Invariance INVARIANT_THEORY):**

Evolution MUST be reversible per §11.4. Meta-invariance requires valid states
remain valid — rollback to previously valid state is valid. Cascade ensures
hierarchy consistency during reversal.

**Popperian Criterion:**
```
F_r = {rollback that changes bedrock OR structure OR leaves children inconsistent}
E_r = {pre-rollback validation: bedrock check + structure check + cascade impact analysis}
```

---

## 14. Cross-Project Interaction Protocol

### 14.1 Project Isolation with Shared Level 1

**Statement:** Shared Level 1 documents across multiple Armatura projects MUST propagate changes consistently with project-isolated cascade.

**Formalization:**
```
CrossProjectCascade(level1_doc, Δ):
    -- Level 1 change affects ALL projects
    affected_projects = FindAllProjectsUsing(level1_doc)
    
    ∀project ∈ affected_projects:
        project_spec = project.specification_doc
        
        -- Each project handles cascade independently
        project_queue = CreateIsolatedQueue(project)
        
        AddToQueue(project_queue, {
            target: project_spec,
            action: "update_parent_ref",
            new_parent_version: level1_doc.new_version,
            project_context: project.id
        })
        
        -- Projects don't interfere with each other
        -- No cross-project dependencies except shared Level 1
        
    Execution:
        ParallelForeach(project ∈ affected_projects):
            ExecuteCascade(project_queue)  -- Isolated per project
            
    Validation:
        ∀project ∈ affected_projects:
            Verify(project.spec.parent_version == level1_doc.new_version)
            Verify(project.spec.structure_valid)  -- Per project standards

ProjectIsolationInvariant:
    ∀project₁, project₂ where project₁ ≠ project₂:
        Cascade(project₁) ⊥ Cascade(project₂)
        
    -- Cascades are orthogonal (independent)
    -- Except for shared Level 1 source of truth
```

**Justification (§3.2 Nomadic Invariant + §10.3 Additivity INVARIANT_THEORY):**

Projects must remain nomadic — independent from each other except for universal
theory. Additivity requires linear complexity: O(n projects) not O(n² interactions).

**Popperian Criterion:**
```
F_r = {cross-project cascade that creates dependencies between projects}
E_r = {manifest validation: project A docs don't reference project B internal docs}
```

---

## Version

*Document: ARMATURA_DOCUMENT_PROTOCOL.md*  
*Version: 1.3.0*  
*Status: Active Protocol*  
*Hierarchy: Level 1 (Universal)*  
*Parent: INVARIANT_THEORY.md v1.3*  
*Siblings: All project specifications*  
*Changes: Added §12 Concurrent Modification Protocol (AI-only), §13 Rollback Protocol (reverse cascade), §14 Cross-Project Protocol (project isolation)*
