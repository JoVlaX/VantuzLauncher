# Deviation Protocol 002: Measurability Violation

**Status:** Resolved 2026-06-03 — All Phases Complete  
**Created:** 2026-06-02T15:45:00+05:00  
**Deadline:** 2026-06-04T23:59:59+05:00  
**Closed:** 2026-06-03T17:58:00+05:00  
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
- [x] Document `GenerateTargetFrameworkAttribute=false` SDK workaround
- [x] Scope verification targets to `Release` only — `dotnet run` (Debug) skips GUI plugin checks
- [x] Add `VerifyManifestValid` target for boot.gui.json validation (superseded by ARM-BUILD-020 VerifyPluginNames target)
- [x] Test all error conditions

### Phase 3: SDK Workaround Documentation (2026-06-02) ✅
- [x] Document `GenerateTargetFrameworkAttribute=false` workaround for WPF SDK CS0579 bug
- [x] Add workaround comment in `VantuzLauncher.csproj`

### Phase 4: Obfuscar Evaluation ✅ Resolved 2026-06-03
- [x] Evaluated Obfuscar 2.2.38 → 2.2.50 for .NET 8 WPF compatibility
- [x] Confirmed Obfuscar 2.2.x cannot load .NET 8 WPF assemblies (GitHub issue #477)
- [x] Removed Obfuscar from build pipeline; documented incompatibility in VantuzLauncher.csproj
- [x] Build-time verification (Phases 1–3, 5–7) satisfies §1.2 Measurability without obfuscation
- [x] Close this deviation protocol

### Phase 7: Complete Boot Manifest Verification (2026-06-03) ✅
- [x] Copy `boot.minecraft.production.json` into build output during `AssembleVantuz`
- [x] Add `ARM-BUILD-009C` existence check for production manifest in output
- [x] Extend MSBuild `VerifyPluginNames` target to verify **all** `boot*.json` manifests via `verify-dir`
- [x] Rewrite `PluginNameVerifier` with **Mono.Cecil static IL analysis** — eliminates `ReflectionTypeLoadException` from WPF dependencies
- [x] GUI plugins (`GUI.MinecraftLauncher`, `GUI.CredentialCollection`) now discoverable at build-time without `PresentationFramework`
- [x] Update `validate-build-paths.ps1` `Assert-PipelineNames` to use `verify-dir`
- [x] Per-manifest reporting: each manifest verified independently with name count

### Phase 6: Plugin Name Verification (2026-06-03) ✅
- [x] Create `verify-plugin-names.ps1` for build-time pipeline-to-plugin cross-reference
- [x] Integrate into MSBuild via `VerifyPluginNames` target (`ARM-BUILD-020`)
- [x] Integrate into `validate-build-paths.ps1` as `Assert-PipelineNames`
- [x] Fix `boot.headless.json` drift (`Game.Installer`→`Game.InstallerCommand`, `Game.VersionValidator`→`Game.VersionValidatorQuery`)
- [x] Fix `boot.minecraft.production.json` drift (`Auth.LoginCommand`→`Auth.YggdrasilCommand`)

### Phase 5: Dual-Path Validation Pipeline (2026-06-03) ✅
- [x] Create `validate-build-paths.ps1` with atomic assertions per INVARIANT_THEORY.md §1.2
  - `Assert-CleanBuild` — deterministic clean + solution build
  - `Assert-DotNetRun` — `dotnet run --project` headless path
  - `Assert-PluginsCopied` — verify all plugin DLLs in output/plugins
  - `Assert-BootJsonIntegrity` — hash pinning verification
- [x] Integrate into `test-and-run.ps1` post-build
- [x] Integrate into `auto-fix-orchestrator.ps1` build phase
- [x] Add CI step in `.github/workflows/build-and-test.yml`
- [x] Document in `docs/verification-checklist.md`

**Rationale:** The original bug (CS0579 + plugin copy order) only manifested on `dotnet run --project`, not `dotnet build`. Manual validation of `dotnet build` alone was insufficient per §1.2 Measurability. The pipeline ensures both paths are tested atomically and automatically.

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Silent build failure | Low | High | Add explicit Error targets |
| Missing file at runtime | Medium | High | Pre-build verification |
| User confusion from errors | Low | Medium | Clear error messages |

## Implementation

```xml
<!-- SDK Workaround: Prevent CS0579 duplicate TargetFrameworkAttribute for WPF net8.0-windows -->
<!-- Per INVARIANT_THEORY.md §9.4 Legacy Compatibility — temporary workaround pending SDK fix -->
<Target Name="RemoveDuplicateFrameworkAttributes" BeforeTargets="BeforeCompile">
  <Delete Files="$(IntermediateOutputPath)*.AssemblyAttributes.cs" ContinueOnError="true" />
</Target>

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
