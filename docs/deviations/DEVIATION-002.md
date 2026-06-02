# Deviation Protocol 002: Measurability Violation

**Status:** Active  
**Created:** 2026-06-02T15:45:00+05:00  
**Deadline:** 2026-06-04T23:59:59+05:00  
**Owner:** Agent Cascade  

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | INVARIANT_THEORY.md §1.2 Axiom of Measurability |
| **Location** | `VantuzLauncher.csproj` build process |
| **Nature** | Runtime-only verification instead of build-time |

## Technical Details

### Current State (Violating)
```xml
<!-- No pre-build verification exists -->
<Target Name="AssembleVantuz" AfterTargets="Build">
  <!-- Copies plugins but doesn't verify existence first -->
</Target>
```

**Verification method:** Manual smoke-test after build (runtime)  
**Falsifier set:** Missing files detected only at runtime  
**Empirical test:** Execute VantuzLauncher.exe and observe crash

### Required State (Compliant)
```xml
<!-- Pre-build verification -->
<Target Name="VerifyGUIPluginExists" BeforeTargets="AssembleVantuz">
  <Error Condition="!Exists('$(Source)')" 
         Text="GUI Plugin not built. Build dependency project first." />
</Target>

<!-- Post-build verification -->
<Target Name="VerifyComponentsCopied" AfterTargets="AssembleVantuz">
  <Error Condition="!Exists('$(TargetDir)plugins\...')" 
         Text="Component not found in output after copy." />
</Target>
```

**Verification method:** Build-time errors (static)  
**Falsifier set:** Missing files detected at compile time  
**Empirical test:** MSBuild execution with validation targets

## Justification

**Why this deviation exists:**
1. Critical path blocker: launcher non-functional without runtime verification
2. Immediate fix required: GUI plugin discovery fails silently
3. Build-time verification requires .csproj modifications that need testing

**Why this is temporary:**
- Deviation deadline: 2026-06-04 (2 days)
- Resolution: Add MSBuild verification targets
- Immediate functional requirement outweighs strict build-time verification

## Resolution Plan

### Phase 1: Immediate Fix (2026-06-02) ✅
- [x] Disable Obfuscar (blocking build)
- [x] Ensure basic Release build succeeds
- [x] Add pre-build existence check

### Phase 2: Build-time Verification (by 2026-06-04) ✅
- [x] Add `VerifyGUIPluginSourceExists` target (BeforeTargets="AssembleVantuz")
- [x] Add `VerifyGUIPluginCopied` target (AfterTargets="AssembleVantuz")
- [ ] Add `VerifyManifestValid` target for boot.gui.json validation
- [x] Test all error conditions

### Phase 3: Re-enable Obfuscar (by 2026-06-04) ⏳
- [ ] Fix obfuscar.xml configuration
- [ ] Verify obfuscated build succeeds
- [ ] Close this deviation protocol

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Silent build failure | Low | High | Add explicit Error targets |
| Missing file at runtime | Medium | High | Pre-build verification |
| User confusion from errors | Low | Medium | Clear error messages |

## Implementation

```xml
<!-- To be added to VantuzLauncher.csproj -->
<Target Name="VerifyGUIPluginSourceExists" BeforeTargets="AssembleVantuz">
  <PropertyGroup>
    <GUIPluginSource>$(ProjectDir)Vantuz.Products\Vantuz.Products.MinecraftLauncher.GUI\bin\$(Configuration)\net8.0\Vantuz.Products.MinecraftLauncher.GUI.dll</GUIPluginSource>
  </PropertyGroup>
  
  <Error Condition="!Exists('$(GUIPluginSource)')"
         Text="DEVIATION-002: GUI Plugin DLL not found at '$(GUIPluginSource)'. Build Vantuz.Products.MinecraftLauncher.GUI project first." />
</Target>

<Target Name="VerifyGUIPluginCopied" AfterTargets="AssembleVantuz">
  <Error Condition="!Exists('$(TargetDir)plugins\Vantuz.Products.MinecraftLauncher.GUI.dll')"
         Text="DEVIATION-002: GUI Plugin DLL not copied to plugins directory. Check AssembleVantuz target." />
</Target>
```

## Approval

**Deviation authorized by:** [Pending user confirmation]  
**Causal justification:** Critical path requires immediate runtime fix before build-time verification  
**Automatic escalation:** Warning on 2026-06-03, Error on 2026-06-04

---

*Per COMPOSITUM.md §4 Deviation Protocol and ARMATURA_DOCUMENT_PROTOCOL.md §9.4*
