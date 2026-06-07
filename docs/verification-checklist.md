# Pre-Commit Verification Checklist

**Per INVARIANT_THEORY.md §2.1: Explicitness Invariant**  
**Per INVARIANT_THEORY.md §3.4: Falsifiability Principle**

Этот чек-лист — **явный контракт** между разработчиком и системой.  
Игнорирование проверок = нарушение архитектурных инвариантов.

---

## Обязательные проверки (перед каждым коммитом)

### 1. Build Verification
```powershell
.\scripts\verify.ps1 -Phase Build
```
- [ ] Сборка завершается без ошибок
- [ ] Все проекты компилируются
- [ ] Нет linker errors

### 1a. Dual-Path Build Validation (INVARIANT_THEORY.md §1.2)
```powershell
.\validate-build-paths.ps1 -AssertAll
```
- [ ] `dotnet build VantuzLauncher.sln -c Release` — проходит
- [ ] `dotnet run --project VantuzLauncher.csproj` — проходит
- [ ] Все plugin DLLs скопированы в `output/plugins`
- [ ] `boot.json` хеши совпадают с актуальными DLL
- [ ] **PipelineNames** — все `pluginName` в `boot*.json` разрешаются в plugin class name
- [ ] **GuiPipeline** — GUI pipeline (13 шагов) загружается без "Plugin X not found"

**Критично:** Регрессия CS0579 / plugin-copy-order возможна только при `dotnet run`, но не при `dotnet build`. Обе проверки обязательны.

### 2. Architectural Analyzers
```powershell
.\scripts\verify.ps1 -Phase Analyzers
```
- [ ] **ARM011** (Component Scope) — нет ошибок
  - Level 4 (Products) не реализует Level 2 (Plugins) напрямую
- [ ] **ARM012/ARM013** (Context Keys) — проверены warnings
  - Нет unmatched context.Set/Get
  - Нет похожих ключей (underscore vs dot)

**Если ARM011 срабатывает:** исправить перед коммитом (критично).  
**Если ARM012/ARM013:** проверить, что это осознанно.

### 2a. Build-Time Invariant Verifiers (per §7.1a V Completeness Dashboard)

- [ ] **ARM-BUILD-021** (DAG Verification) — `PipelineVisualizer.cs` содержит `DetectCycle()`
  - `F_doc` = `{PipelineVisualizer.cs без "DetectCycle"}`
  - `E_doc` = `{Select-String "DetectCycle" PipelineVisualizer.cs}`
- [ ] **ARM-BUILD-022** (CQRS Separation) — `PluginNameVerifier.cs` проверяет Command/Query разделение
  - `F_doc` = `{PluginNameVerifier.cs без "VerifyCQRS"}`
  - `E_doc` = `{Select-String "VerifyCQRS" PluginNameVerifier.cs}`
- [ ] **ARM-BUILD-023** (Resource Category) — `PluginNameVerifier.cs` сканирует `FileStream`/`HttpClient`/`Process`
  - `F_doc` = `{PluginNameVerifier.cs без "ForbiddenResourceTypes"}`
  - `E_doc` = `{Select-String "ForbiddenResourceTypes" PluginNameVerifier.cs}`
- [ ] **ARM-BUILD-024** (Scope Verification) — `PluginNameVerifier.cs` проверяет cross-assembly references
  - `F_doc` = `{PluginNameVerifier.cs без "VerifyScope"}`
  - `E_doc` = `{Select-String "VerifyScope" PluginNameVerifier.cs}`
- [ ] **ARM-BUILD-025** (Assembly Classification) — `Vantuz.Builder.csproj` содержит classification comment
  - `F_doc` = `{Vantuz.Builder.csproj без "Category-level build tooling"}`
  - `E_doc` = `{Select-String "Category-level build tooling" Vantuz.Builder.csproj}`
- [ ] **ARM-BUILD-026** (Nomadic/Transdomain Primitives) — `PluginNameVerifier.cs` содержит `VerifyNomadic`
  - `F_doc` = `{PluginNameVerifier.cs без "VerifyNomadic"}`
  - `E_doc` = `{Select-String "VerifyNomadic" PluginNameVerifier.cs}`
- [ ] **ARM-BUILD-027** (V Completeness Report) — build output содержит `V_completeness_report.json`
  - `F_doc` = `{build output без V_completeness_report.json}`
  - `E_doc` = `{Test-Path output/V_completeness_report.json после build}`

### 3. Deviation Review
- [ ] Все DEVIATION протоколы проверены
  - [x] [DEVIATION-001](../../docs/deviations/DEVIATION-001.md) — Component Scope Violation (Resolved)
  - [x] [DEVIATION-002](../../docs/deviations/DEVIATION-002.md) — Build-Time Verification (Resolved)
  - [x] [DEVIATION-005](../../docs/deviations/DEVIATION-005.md) — Partial Loader Implementation (Resolved 2026-06-03)
  - [x] [DEVIATION-006](../../docs/deviations/DEVIATION-006.md) — DAGVerifier Missing (Resolved 2026-06-03)
  - [x] [DEVIATION-007](../../docs/deviations/DEVIATION-007.md) — NomadicVerifier Missing (Resolved 2026-06-03)
- [ ] Дедлайны не просрочены
- [ ] Протоколы обновлены (если статус изменился)

---

## Расширенные проверки (перед важными коммитами)

### 4. Smoke Test
```powershell
.\scripts\verify.ps1 -Phase Smoke
```
- [ ] Все required файлы присутствуют
- [ ] Boot manifests — валидный JSON
- [ ] Plugin DLLs скопированы в output

### 5. Runtime Verification

**Automated (headless / CI):**
- [ ] Headless pipeline (`boot.headless.json`) — `HeadlessSmokeTests` + `PipelinePositiveVerificationTests`
- [ ] GUI pipeline **resolution** (`boot.json`) — `GuiPipelinePositiveVerificationTests` (13 plugin names load without "Plugin X not found")
- [ ] Launch argument **validation** — `LaunchArgumentValidationTests` (pre-flight checks: installDir exists, Java exists, no unresolved placeholders)
- [ ] Variable interpolation **with dependencies** — `VariableInterpolationTests` (`mcDir → installDir → gameArgs` chain resolves without `{{...}}` leakage)
- [ ] Version detection **logic** — `MinecraftGameProviderTests` (`CheckVersionAsync` with fake `versions/{ver}/{ver}.json`, `ParseForgeVersion` correctness, skip-if-installed end-to-end)

**Manual (only the user can perform):**
- [ ] Launcher запускается без критических ошибок
- [ ] GUI отображается
- [ ] Кнопка «Играть» инициирует pipeline без crash

> **Confidence Boundary (updated 2026-06-07):**
> - AI can verify **static correctness** (compilation, name resolution, hash integrity)
> - AI can verify **headless pipeline execution** (5 steps, `dryRun=true`)
> - AI can verify **GUI pipeline resolution** (13 plugin names → loaded nodes)
> - AI can verify **launch argument pre-flight** (paths exist, placeholders resolved)
> - AI can verify **variable interpolation with dependencies** (`mcDir → installDir`, `${special:Folder}` → `{{key}}`)
> - AI can verify **version detection logic** (`CheckVersionAsync`, `ParseForgeVersion`, skip-if-installed)
> - AI **cannot verify** that the actual Java/Minecraft process launches successfully in the user's environment
> - AI **cannot verify** that real Forge installation over the internet completes within the timeout
> - Green `GuiPipeline` test does NOT prove the Java process launches — it only proves plugin names resolve. The manual surface is reduced to "does the real game launch?"
>
> **Recidivism lessons:**
> 1. A `GuiPipeline PASS` was incorrectly interpreted as "GUI pipeline works end-to-end" while the actual crash occurred at `OS.ExecuteCommand`. The test timeout (3 sec) cancelled the pipeline before reaching step 12-13. Closed by `LaunchArgumentValidationTests`.
> 2. `LaunchArgumentValidationTests PASS` + `GuiPipeline PASS` still missed a bug because `InterpolateVariables` was never tested with dependent variables (`mcDir → installDir`). The literal `{{mcDir}}` leaked into Java arguments. Closed by `VariableInterpolationTests`.
> 3. `VariableInterpolationTests PASS` + all prior tests still missed a bug because the **real `ForgeInstaller.Install` path with network I/O** was only exercised at manual runtime. The mock-based `ForgeInstallTimeoutRecidivismTests` proved the command respects a timeout, but did NOT prove the installer works. Closed by `MinecraftGameProviderTests` (version detection) + `[DIAG ...]` runtime logging.
> 4. **The 5-minute Forge timeout was a guess without empirical data.** User observed Forge install takes 40-50 minutes on their connection. The timeout was set to "5 minutes" because it "seemed reasonable." "Reasonable" timeouts without measurement are bugs. Closed by increasing `operationTimeout` to 60 min and removing the 30-sec false-stall watchdog.
> 5. **A green test suite does NOT prove the deployed binary is rebuilt.** After any engine-level fix, the user must rebuild (`dotnet build`) and redeploy.

---

## Запрещённые механизмы

Следующие механизмы **нарушают INVARIANT_THEORY.md §1.1 (Negative Ontology)**:

- ❌ Git Pre-Commit Hooks (скрытые, неявные)
- ❌ IDE Extensions с "магическим" поведением
- ❌ Скрытые environment-based проверки
- ❌ Любая автоматизация, невидимая в codebase

**Используйте вместо этого:**
- ✅ Явные build scripts (`scripts/verify.ps1`)
- ✅ Roslyn Analyzers (ARM007-ARM013) — видимы в build output
- ✅ Build-Time Targets — явно объявлены в `.csproj`
- ✅ Этот чек-лист — явный контракт

---

## Проверка листа

Перед коммитом:
1. Запустить `.\scripts\verify.ps1`
2. Отметить выполненные пункты
3. Убедиться, что все обязательные проверки пройдены
4. Коммитить только после ✓

**Подпись разработчика:** _________________ Дата: _________

---

## Статистика нарушений

| Дата | Тип нарушения | Исправление | DEVIATION |
|------|---------------|-------------|-----------|
| | | | |

---

*Последнее обновление: 2026-06-07*
