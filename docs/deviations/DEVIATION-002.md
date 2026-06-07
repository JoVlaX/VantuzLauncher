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

### Phase 8: Runtime Crash Retrospective (2026-06-03–06-07) ✅
- [x] **Root cause identified:** `Plugin Net.ApiReaderQuery not found` — `boot.json pipeline[].pluginName` drifted from plugin class `Name` property (`Net.ApiReader`)
- [x] **Systemic audit:** 11 plugins had similar drift (`Net.Update`→`Net.UpdateCommand`, `OS.Execute`→`OS.ExecuteCommand`, etc.)
- [x] **Fix:** Aligned all plugin `Name` properties with `boot.json` pipeline references
- [x] **Secondary fix:** `GameInstallerCommand.cs:115` — `timeout` variable used in `catch` block but declared inside `try` scope (CS0103). Moved declaration before `try`
- [x] **Why validation missed it:** `dotnet build` doesn't verify string semantics; `boot.headless.json` uses smaller pipeline that didn't include `Net.ApiReaderQuery`; no build-time cross-reference existed between pipeline names and plugin class names
- [x] **Prevention:** `ARM-BUILD-020` `VerifyPluginNames` MSBuild target now fails the build if any pipeline `pluginName` doesn't resolve to a discovered plugin class
- [x] **Validation:** `validate-build-paths.ps1 -AssertAll` now includes `Assert-PipelineNames` covering both `boot.json` (GUI) and `boot.headless.json` (headless)

### Phase 9: GUI Pipeline Automated Verification (2026-06-07) ✅
- [x] **Problem identified:** AI cannot execute `VantuzLauncher.exe` or click "Играть". Confidence in "GUI works" was based on headless tests (5 steps) that never touched the GUI pipeline (13 steps).
- [x] **Solution:** `GuiPipelinePositiveVerificationTests.cs` — headless test that loads `boot.json`, instantiates `VantuzEngine`, and via reflection invokes `BuildQuantumPipeline` to prove all 13 plugin names resolve to loaded `QuantizedNode` instances.
- [x] **Secondary test:** `GuiPipeline_ExecutesWithoutPluginNotFoundCrash` — runs `RunAsync` with a 3-second cancellation to prove the engine starts the pipeline and does NOT crash with "Plugin X not found".
- [x] **Integration:** `validate-build-paths.ps1 -AssertGuiPipeline` runs `dotnet test --filter GuiPipelinePositiveVerificationTests` and reports in the validation summary.
- [x] **Confidence Boundary documented:**
  - AI can verify: static correctness (compilation, name resolution, hash integrity), headless pipeline execution (5 steps), GUI pipeline resolution (13 steps via reflection)
  - Only user can verify: interactive GUI button-click behavior
  - Manual surface reduced to a single interactive check per release.

### Phase 10: Test Coverage Gap — Tests Pass but Java Crashes (2026-06-07) ✅
- [x] **Crash reported:** `[2026-06-07 10:25:11] Pipeline failed: Процесс крашнулся при запуске (ExitCode: 1)` after clicking "Играть".
- [x] **Why tests missed it:**
  - `GuiPipeline_ResolvesAllPlugins` only checks `BuildQuantumPipeline` (name resolution), not step execution.
  - `GuiPipeline_ExecutesWithoutPluginNotFoundCrash` uses a 3-second timeout — pipeline with 13 steps + HTTP requests never reaches step 12 (`Game.LaunchCommand`) → 13 (`OS.ExecuteCommand`).
  - No test verified that `OS.ExecuteCommand` arguments are valid before `Process.Start`.
- [x] **Recidivism root cause:** `GuiPipeline PASS` was incorrectly interpreted as "GUI pipeline works end-to-end" when it only proved "plugin names load without exception".
- [x] **Fix — Pre-flight validation in `Game.LaunchCommand`:**
  - Check `installDir` exists before calling provider
  - Check `javaPath` exists (or resolves in PATH)
  - Check `authlibPath` exists (if provided)
- [x] **Fix — Early failure in `OS.ExecuteCommand`:**
  - Fail fast if `fileName`, `arguments`, or `workDir` contain unresolved `{{...}}` placeholders
  - Log full command line in crash error message
  - Capture stderr even in `waitForExit=false` mode for diagnostics
- [x] **Fix — New tests:** `LaunchArgumentValidationTests.cs`
  - `MissingInstallDir_ReturnsFailureWithClearMessage`
  - `MissingJava_ReturnsFailureWithClearMessage`
  - `UnresolvedPlaceholder_ReturnsFailureWithClearMessage`
  - `DummyExecutable_ReturnsSuccess` (proves `OS.ExecuteCommand` can launch a real process)
- [x] **Confidence Boundary updated:** Green `GuiPipeline` test does NOT prove Java launches. Pre-flight checks and `LaunchArgumentValidationTests` close the gap between "names resolve" and "arguments are valid". Only manual QA can verify real Java/Minecraft execution.

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
