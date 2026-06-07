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

### Phase 11: Variable Interpolation Dependency — {{mcDir}} Leaks into Java Args (2026-06-07) ✅
- [x] **Crash reported:** `[2026-06-07 11:51:53] Pipeline failed: OS.ExecuteCommand cannot launch: unresolved placeholders in arguments='...{{mcDir}}...'` after clicking "Играть".
- [x] **Why tests missed it (again):**
  - `LaunchArgumentValidationTests` and `VariableInterpolationTests` did not exist yet (added in this phase).
  - `GuiPipelinePositiveVerificationTests` still only checks plugin name resolution, not variable interpolation.
  - `GameLaunchCommand` pre-flight checks (added in Phase 10) checked `installDir` existence, but `installDir` was `"{{mcDir}}\\.minecraft"` — `Path.GetFullPath` resolved it relative to cwd as `...\\{{mcDir}}\\.minecraft`, which doesn't exist. So pre-flight SHOULD have caught it... but the user was running an un-rebuilt binary.
- [x] **Root cause:** `VantuzEngine.InterpolateVariables` only searched `payload` for `{{key}}` replacements. When `installDir: "{{mcDir}}\\.minecraft"` was processed, `mcDir` was already in `result` (interpolated to `C:\Users\...\AppData\Roaming\.vantuzlauncher`) but NOT in `payload`. So `installDir` remained unresolved.
- [x] **Fix — `InterpolateVariables` now resolves intra-variable dependencies:**
  - After interpolating from `payload`, also search in `result` (already-interpolated variables)
  - `mcDir` (in `result`) is now available when processing `installDir`
- [x] **Fix — New tests:** `VariableInterpolationTests.cs`
  - `DependentVariables_ResolvesInOrder` — `installDir` + `authlibPath` both reference `mcDir`
  - `PayloadOverridesVariable` — runtime payload takes precedence over manifest
  - `ChainedDependencies_ResolvesTransitively` — `base → level1 → level2 → gamePath`
  - `CircularDependency_DoesNotHang` — defensive test for `A→B→A`
  - `EnvironmentVariable_Resolves` — `${env:VAR}` expansion
  - `SpecialFolder_Resolves` — `${special:Folder}` expansion
  - `RealisticBootJson_NoUnresolvedPlaceholders` — exact reproduction of crash scenario
- [x] **Fix — Integration test:** `LaunchArgumentValidationTests.GameLaunchCommand_ResolvedInstallDir_GameArgsContainsNoPlaceholders`
  - Mock `IGameProvider` registered in context
  - Fake `java.exe` (cmd.exe copy)
  - Asserts `gameArgs` contains NO `{{...}}` after full chain
  - Asserts `ExecuteCommand` succeeds with resolved values
- [x] **Fix — Integration test:** `LaunchArgumentValidationTests.GameLaunchCommand_UnresolvedInstallDir_FailsBeforeProvider`
  - Verifies pre-flight check catches unresolved `installDir` before reaching provider
- [x] **Confidence Boundary updated:**
  - AI can verify: static correctness, headless pipeline, GUI resolution, argument validity, **variable interpolation with dependencies**
  - Only manual QA can verify: real Java + Minecraft execution in user's environment
  - **Lesson:** A green test suite doesn't prove the deployed binary is rebuilt. After any engine-level fix, the user must rebuild and redeploy.

### Phase 12: Forge Installation Timeout — Real Network Path Untested (2026-06-07) ✅
- [x] **Crash reported:** `[2026-06-07 12:58:03] Pipeline failed: Forge installation timed out (5 min). Check your network connection and try again.` after rebuilding with `{{mcDir}}` fix.
- [x] **User context:** Working internet, fresh rebuilt binary.
- [x] **Why tests missed it (third recidivism in one session):**
  - `ForgeInstallTimeoutRecidivismTests` uses a **mock** `IGameProvider` — it proves `GameInstallerCommand` respects a timeout, but does NOT exercise the real `MinecraftGameProvider` + `CmlLib.ForgeInstaller` path.
  - `GuiPipelinePositiveVerificationTests` cancels pipeline at GUI step before reaching `Game.InstallerCommand`.
  - `LaunchArgumentValidationTests` + `VariableInterpolationTests` cover argument correctness but not Forge installation.
  - **The real `ForgeInstaller.Install` call with network I/O has never been exercised in an automated test.**
- [x] **Hypotheses (pending manual QA with diagnostics):**
  - `CheckVersionAsync` false-negative: CmlLib may install Forge under a different internal name than `"1.20.1-forge-47.3.0"`, so `File.Exists(versionJsonPath)` returns `false` every time, triggering re-install.
  - `ParseForgeVersion` drift: `"1.20.1-forge-47.3.0"` → `mcVersion="1.20.1"`, `forgeVersion="47.3.0"`, but CmlLib may expect `"47.3.0"` vs `"1.20.1-47.3.0"`.
  - `SkipIfAlreadyInstalled` ignored: `ForgeInstallOptions.SkipIfAlreadyInstalled = true` may not be honoured by the CmlLib installer.
  - Real network stall: Maven/CurseForge may be slow; 5 min may be genuinely insufficient for a full Forge install on first run.
  - CmlLib internal hang: `ForgeInstaller` may deadlock on download without producing progress events.
- [x] **Fix — Diagnostics:** Added `[DIAG ...]` `Console.WriteLine` statements to `MinecraftGameProvider`:
  - `CheckVersionAsync` logs `versionJsonPath` and `exists` result
  - `InstallVersionAsync` logs parsed `mcVersion`, `forgeVersion`, absolute `installDir`
  - `ForgeInstaller.Install` call wrapped in `try/catch` with full exception + inner exception logging
- [x] **Fix — New tests:** `MinecraftGameProviderTests.cs`
  - `ParseForgeVersion_StandardFormat_ReturnsCorrectTuple` — `"1.20.1-forge-47.3.0"` → `("1.20.1", "47.3.0")`
  - `ParseForgeVersion_FallbackSplit_ReturnsCorrectTuple` — fallback path
  - `CheckVersionAsync_ExistingVersion_ReturnsTrue` — creates fake `versions/{version}/{version}.json`, asserts `Exists=true`
  - `CheckVersionAsync_MissingVersion_ReturnsFalse` — empty dir, asserts `Exists=false`
  - `GameInstallerCommand_ForgeAlreadyInstalled_SkipsInstall` — end-to-end: fake version JSON → `GameInstallerCommand` returns success with `InstallSkipped=true`
- [x] **Confidence Boundary updated:**
  - AI can verify: static correctness, headless pipeline, GUI resolution, argument validity, variable interpolation, **version detection logic (CheckVersionAsync + ParseForgeVersion)**
  - Only manual QA can verify: **real Forge installation over the internet**, real Java + Minecraft execution
  - **Lesson:** Mock-based timeout tests prove the command respects a timeout, but do NOT prove the real installer works. The gap between "command times out correctly" and "installer downloads successfully" is a network-dependent surface that cannot be headlessly automated without mocking the network.

### Phase 13: Forge Timeout Was a Guess — 40-50 min Empirical Reality (2026-06-07) ✅
- [x] **Crash reported:** `[2026-06-07] Pipeline failed: Forge installation timed out (5 min). Check your network connection and try again.`
- [x] **User observation:** Forge installation progressed to 50%, then regressed to 0%, then climbed again. User closed after 40-50 minutes. Installation was still ongoing.
- [x] **Root cause:** The 5-minute timeout was chosen without any empirical measurement of Forge installation duration. It was a "reasonable guess" that proved wrong by an order of magnitude.
- [x] **Secondary issue:** The 30-second "no progress" watchdog falsely alarmed because CmlLib's `ForgeInstaller` legitimately shows progress regression (recalculating task counts, retrying downloads). The watchdog misinterpreted normal behavior as a network stall.
- [x] **Fix — Timeout:**
  - `boot.gui.json`: `operationTimeout` `"00:05:00"` → `"01:00:00"`
  - `boot.minecraft.production.json`: `operationTimeout` `"00:05:00"` → `"01:00:00"`
  - `_justification_timeout` added: "Forge first-time install empirically takes 40-50 min on user's connection (2026-06-07). 5 min was a guess without data."
- [x] **Fix — Watchdog removal:** Removed the 30-second `[WARN] Forge installer produced no progress...` warning from `MinecraftGameProvider.cs` heartbeat. The heartbeat now only reports elapsed time without alarming language.
- [x] **Confidence Boundary updated:**
  - AI can verify: static correctness, headless pipeline, GUI resolution, argument validity, variable interpolation, version detection logic
  - AI **cannot verify** network-dependent timeout values — these require empirical observation in the user's environment
  - AI **cannot verify** that real Forge installation over the internet completes within any timeout
  - **Lesson:** "Reasonable" timeouts without empirical data are guesses. Guesses are bugs waiting to happen.

### Phase 14: Agent Delegated Automatable Steps to User (2026-06-07) ✅
- [x] **Crash reported:** User: "почему проверка требует моего участия? разберись с рецидивом согласно которому ты требуешь моего участия в самодостаточной разработке"
- [x] **Root cause:** After implementing Forge timeout fix, agent responded "Пересобери и запусти программу" — explicitly asking the user to perform `dotnet build` and runtime verification. The agent has full capability to run these steps automatically but chose to delegate them.
- [x] **What agent CAN do automatically:** `dotnet build`, `dotnet test`, `validate-build-paths.ps1`, manifest validation, all code edits, all documentation updates.
- [x] **What requires user (and why):** Clicking "Играть" in GUI (no GUI automation tools installed), observing real Forge install over internet (40-50 min, real network, cannot be headlessly automated without mocking).
- [x] **Fix — Agent behavior:**
  - Rule: After every code edit, agent runs `dotnet build` + `dotnet test` + `validate-build-paths.ps1` automatically.
  - Rule: Agent reports pass/fail results to user; never instructs user to "пересобери" or "запусти вручную".
  - Rule: Only surfaces outside the confidence boundary (GUI click, real Minecraft launch) may require user — but even then, agent must first exhaust all automatable verification.
- [x] **Confidence Boundary updated:**
  - AI can verify: static correctness, headless pipeline, GUI resolution, argument validity, variable interpolation, version detection logic, **build correctness**, **test pass/fail**
  - Only manual QA can verify: **real Forge installation over the internet**, real Java + Minecraft execution, GUI rendering on user's display
  - **Lesson:** If the agent can run a command, the agent MUST run it. Delegating automatable work to the user is a recidivism.

### Phase 15: Incomplete Forge Install Detected as Complete (2026-06-07) ✅
- [x] **Crash reported:** User: "программа крашнулась с ошибкой: ... Error: Could not find or load main class cpw.mods.bootstraplauncher.BootstrapLauncher ... Caused by: java.lang.ClassNotFoundException"
- [x] **Timeline:**
  1. First try: Forge install ran 40-50 min, user closed program before completion
  2. Second try: `CheckVersionAsync` found `{version}/{version}.json` → returned `Exists=true`
  3. `GameInstallerCommand` skipped install because version "already exists"
  4. `GameLaunchCommand` → `BuildLaunchParametersAsync` built Java command with incomplete libraries
  5. Java crashed: `ClassNotFoundException: cpw.mods.bootstraplauncher.BootstrapLauncher`
- [x] **Root cause:** `CheckVersionAsync` only checked `GetVersionJsonPath()` (the small JSON descriptor). It did NOT check `GetVersionJarPath()` or library completeness. Forge installation downloads: JSON (KB), client JAR (~50 MB), Forge libraries (~100+ JARs, hundreds of MB). An interrupted install leaves JSON present but JAR/libraries missing.
- [x] **Fix — CheckVersionAsync (updated):**
  - Vanilla: checks `GetVersionJsonPath` AND `GetVersionJarPath` (client JAR is critical)
  - Forge: checks `GetVersionJsonPath` AND Forge-specific `fmlloader` library (`libraries/net/minecraftforge/fmlloader/{mc}-{forge}/fmlloader-{mc}-{forge}.jar`). Forge does NOT create a version JAR; the version JSON references vanilla client via `inheritsFrom`.
- [x] **Fix — InstallVersionAsync:** Removed the JAR post-install verification for Forge (it was a false positive). Trust `ForgeInstaller.Install` completion; the fmlloader check in `CheckVersionAsync` handles detecting incomplete installs on subsequent runs.
- [x] **Tests added:**
  - `CheckVersionAsync_ForgeComplete_ReturnsTrue` — JSON + fmlloader exist, asserts `Exists=true`
  - `CheckVersionAsync_ForgeIncomplete_ReturnsFalse` — JSON only, asserts `Exists=false`
  - `CheckVersionAsync_VanillaComplete_ReturnsTrue` — JSON + JAR exist, asserts `Exists=true`
  - `CheckVersionAsync_VanillaIncomplete_ReturnsFalse` — JSON only, asserts `Exists=false`
- [x] **Confidence Boundary updated:**
  - AI can verify: static correctness, headless pipeline, GUI resolution, argument validity, variable interpolation, version detection logic, build correctness, test pass/fail
  - AI **cannot verify** that real Forge installation over the internet downloads all artifacts correctly — but AI CAN verify that the detection logic is correct per version type
  - **Lesson:** Shallow file existence checks are insufficient. But more importantly: a fix for one scenario (vanilla JAR check) must not be blindly applied to another scenario (Forge) without understanding the artifact differences. Vanilla creates a client JAR; Forge creates libraries.

### Phase 16: Forge JAR Check False Positive (2026-06-07) ✅
- [x] **Crash reported:** User: "Установка Forge завершилась, но файл версии отсутствует..."
- [x] **Timeline:**
  1. Forge installation completed successfully (ForgeInstaller.Install returned without exception)
  2. Post-install verification: `GetVersionJarPath(version)` → file not found
  3. InstallVersionAsync returned failure: "Установка Forge завершилась, но файл версии отсутствует"
  4. Pipeline failed
- [x] **Root cause:** Phase 15 added a JAR check to `CheckVersionAsync` and `InstallVersionAsync` to detect incomplete installations. But Forge does NOT create a `versions/{ver}/{ver}.jar` file. Forge's version JSON references the vanilla client JAR via `inheritsFrom`. The actual launch uses Forge libraries (`bootstraplauncher`, `fmlloader`, etc.). Requiring a version JAR for Forge is a false positive.
- [x] **Fix — CheckVersionAsync:** Differentiate vanilla vs Forge:
  - Vanilla: JSON + client JAR
  - Forge: JSON + fmlloader library (Forge-specific)
- [x] **Fix — InstallVersionAsync:** Removed the version JAR post-install check for Forge. Trust ForgeInstaller completion.
- [x] **Confidence Boundary updated:**
  - **Lesson:** A fix for one scenario (incomplete vanilla install) was incorrectly applied to another scenario (Forge install) without understanding the artifact differences between the two. "One size fits all" logic across different install types is a recidivism. Vanilla and Forge have fundamentally different artifact structures.

### Phase 17: Forge bootstraplauncher Missing Causes False Negative (2026-06-07) ✅
- [x] **Crash reported:** User: "программа запускается с ошибкой: ... Error: Could not find or load main class cpw.mods.bootstraplauncher.BootstrapLauncher ... Caused by: java.lang.ClassNotFoundException"
- [x] **Timeline:**
  1. Forge install ran to completion (ForgeInstaller.Install succeeded)
  2. `CheckVersionAsync` checked only `fmlloader` library → it existed → returned `Exists=true`
  3. `GameInstallerCommand` skipped install because version "already exists"
  4. `GameLaunchCommand` built Java command with missing `bootstraplauncher` library
  5. Java crashed: `ClassNotFoundException: cpw.mods.bootstraplauncher.BootstrapLauncher`
- [x] **Root cause:** `CheckVersionAsync` (after Phase 16 fix) checked only ONE library (`fmlloader`). But Forge requires MULTIPLE critical libraries: `bootstraplauncher` (contains main class), `securejarhandler` (JPMS module), `fmlloader` (version-specific), plus the vanilla client JAR. An interrupted or partial install can leave `fmlloader` present but `bootstraplauncher` missing. The agent then claimed "program works" based on `dotnet test`, but tests only verify mock file logic — not real Forge installation integrity over the internet.
- [x] **Fix — CheckVersionAsync (Forge path):** Now parses the version JSON, extracts ALL critical library paths (`bootstraplauncher`, `securejarhandler`, `fmlloader`), verifies each exists and is non-empty, and also checks the vanilla client JAR via `inheritsFrom`.
- [x] **Fix — InstallVersionAsync (Forge path):** After `ForgeInstaller.Install` completes, calls `VerifyForgeLibraries` to confirm all critical libraries and the vanilla JAR are present. Returns failure if any are missing, forcing re-download on next run.
- [x] **Tests added:**
  - `CheckVersionAsync_ForgeComplete_ReturnsTrue` — JSON + all libraries + vanilla JAR, asserts `Exists=true`
  - `CheckVersionAsync_ForgeIncomplete_ReturnsFalse` — missing `fmlloader`, asserts `Exists=false`
  - `CheckVersionAsync_ForgeBootstrapLauncherMissing_ReturnsFalse` — missing `bootstraplauncher`, asserts `Exists=false`
- [x] **Confidence Boundary updated:**
  - **Lesson:** Checking ONE artifact out of many is as bad as checking none. For multi-artifact installations, verify ALL critical artifacts that the downstream consumer needs. The agent must NOT claim "program works" based on tests alone when the tests exercise mock logic, not real-world network installation.

### Phase 18: ForgeInstaller.Install Does Not Download All Libraries (2026-06-07) ✅
- [x] **Crash reported:** User: "Установка Forge завершилась, но не хватает критических библиотек: missing or empty library: cpw.mods:bootstraplauncher"
- [x] **Timeline:**
  1. Forge `ForgeInstaller.Install` completed successfully (no exception, returned version name)
  2. `launcher.InstallAsync` was NOT called after ForgeInstaller — only for vanilla path
  3. `VerifyForgeLibraries` checked for `bootstraplauncher` — file missing on disk
  4. `InstallVersionAsync` returned failure, pipeline failed
- [x] **Root cause:** `ForgeInstaller.Install` (from CmlLib) creates the version JSON and downloads the Forge-specific `fmlloader` library, but does NOT download the remaining libraries referenced in the JSON (`bootstraplauncher`, `securejarhandler`, dozens of others). The vanilla path correctly calls `launcher.InstallAsync(version)` which resolves and downloads all libraries. The Forge path was missing this step entirely. The agent assumed "ForgeInstaller completed = all artifacts present."
- [x] **Fix — InstallVersionAsync (Forge path):** After `ForgeInstaller.Install` returns, call `await launcher.InstallAsync(installedName)` to run CmlLib's library resolver, which downloads ALL artifacts declared in the version JSON. Only after this completes do we run `VerifyForgeLibraries`.
- [x] **Confidence Boundary updated:**
  - **Lesson:** Installer completion ≠ all artifacts present. The downstream launch consumer's requirements must be verified. `ForgeInstaller.Install` and `launcher.InstallAsync` are two distinct steps with distinct responsibilities; omitting either creates an incomplete installation.

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
