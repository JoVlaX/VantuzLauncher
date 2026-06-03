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
- [ ] **ARM-BUILD-026** (Nomadic/Transdomain Primitives) — `DEVIATION-007.md` active или verifier implemented
  - `F_doc` = `{PluginNameVerifier.cs без "TransdomainPrimitive" AND docs/deviations/DEVIATION-007.md missing}`
  - `E_doc` = `{Test-Path docs/deviations/DEVIATION-007.md}`
- [ ] **ARM-BUILD-027** (V Completeness Report) — build output содержит `V_completeness_report.json`
  - `F_doc` = `{build output без V_completeness_report.json}`
  - `E_doc` = `{Test-Path output/V_completeness_report.json после build}`

### 3. Deviation Review
- [ ] Все активные DEVIATION протоколы проверены
  - [DEVIATION-001](../../docs/deviations/DEVIATION-001.md) — Component Scope Violation
  - [DEVIATION-002](../../docs/deviations/DEVIATION-002.md) — Build-Time Verification
  - [DEVIATION-005](../../docs/deviations/DEVIATION-005.md) — Partial Loader Implementation
  - [DEVIATION-006](../../docs/deviations/DEVIATION-006.md) — DAGVerifier Missing
  - [DEVIATION-007](../../docs/deviations/DEVIATION-007.md) — NomadicVerifier Missing
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

### 5. Runtime Verification (вручную)
- [ ] Launcher запускается без критических ошибок
- [ ] Pipeline инициализируется
- [ ] GUI (если применимо) отображается
- [ ] Основной сценарий работает

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

*Последнее обновление: 2026-06-03*
