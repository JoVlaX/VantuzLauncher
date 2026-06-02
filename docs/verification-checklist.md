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

### 3. Deviation Review
- [ ] Все активные DEVIATION протоколы проверены
  - [DEVIATION-001](../../docs/deviations/DEVIATION-001.md) — Component Scope Violation
  - [DEVIATION-002](../../docs/deviations/DEVIATION-002.md) — Build-Time Verification
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

*Последнее обновление: 2026-06-02*
