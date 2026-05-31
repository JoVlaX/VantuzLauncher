---
description: Automatic testing and fixing after code changes
---

# Auto Test & Fix Protocol

Автоматический цикл тестирования и исправления ошибок после изменений кода.

## Trigger Conditions

**ЗАПУСКАТЬ автотесты при:**
- Изменения в `*.cs` файлах
- Изменения в `*.csproj` файлах
- Изменения в `*.sln` файлах

**НЕ ЗАПУСКАТЬ при:**
- Изменения только в `*.md` файлах
- Изменения в `docs/` (только документация)
- Изменения в `.github/` (CI конфиги)

## Execution Protocol

### Step 1: Build
```powershell
dotnet build VantuzLauncher.sln -c Release --verbosity minimal
```
- **Success (exit 0)**: Proceed to Step 2
- **Failure**: Analyze errors → Apply fix → Retry (max 10 iterations)

### Step 2: Headless Test
```powershell
.\test-and-run.ps1 -NoBuild -Timeout 60
```
- **Success (exit 0)**: Report success to user
- **Failure**: Analyze `test-result.json` → Apply fix → Retry

## Error Classification

| Error Pattern | Auto-Fix Strategy |
|---------------|-------------------|
| `CS1002: ; expected` | Add missing semicolon |
| `CS0103: name does not exist` | Check namespace/using |
| `CS1503: cannot convert` | Check type compatibility |
| `CS0246: type not found` | Add using directive |
| `boot.json not found` | Check path construction |
| `NullReferenceException` | Add null check |
| `FileNotFoundException` | Verify file existence before access |
| Timeout / Network | Report as external issue |

## Retry Limits (Termination Guarantee)

```
MaxIterations: 10
StagnationDetection: 3 identical errors in a row
CodeChangeRequired: Must produce diff, else stop
```

## Output Format

**Success:**
```
✅ Auto-test passed
Build: OK
Runtime: OK (duration: Xs)
Iterations: 1
```

**After Fix:**
```
⚠️ Auto-test required fixes
Build: Fixed (missing using)
Runtime: OK
Iterations: 2
Changes made: +2/-1 lines
```

**Failure (max iterations reached):**
```
❌ Auto-test failed after 10 attempts
Last error: [error message]
Unable to auto-fix. Manual intervention required.
```

## Integration with Cascade

After providing work:
1. Check if changes match trigger conditions
2. If yes → Execute this protocol
3. Report final result to user
4. Include summary of fixes applied (if any)
