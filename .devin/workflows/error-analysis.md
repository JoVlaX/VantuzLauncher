---
description: Error analysis and fix decision matrix
---

# Error Analysis Protocol

Алгоритм анализа ошибок и принятия решений об автоматическом исправлении.

## Build Errors (Roslyn)

### Syntax Errors — AUTO-FIX
| Code | Message Pattern | Fix Action |
|------|-----------------|------------|
| CS1002 | `; expected` | Insert `;` at error location |
| CS1513 | `} expected` | Add missing closing brace |
| CS1022 | Type definition in wrong place | Check namespace placement |
| CS1043 | `{` or `}` expected | Fix block delimiters |

### Type/Namespace Errors — AUTO-FIX
| Code | Message Pattern | Fix Action |
|------|-----------------|------------|
| CS0246 | `type/namespace not found` | Add `using X;` or full qualification |
| CS0103 | `name does not exist in context` | Check variable scope, add declaration |
| CS1061 | `does not contain definition` | Check method name, add `using` |
| CS0234 | `type/namespace does not exist` | Fix namespace or add reference |

### Type Compatibility — MANUAL REVIEW
| Code | Message Pattern | Action |
|------|-----------------|--------|
| CS1503 | `cannot convert X to Y` | Analyze intent, may need cast or redesign |
| CS0266 | `cannot implicitly convert` | Check if explicit cast is safe |
| CS0121 | `call is ambiguous` | Explicitly specify type arguments |

### Access/Visibility — MANUAL REVIEW
| Code | Message Pattern | Action |
|------|-----------------|--------|
| CS0122 | `inaccessible due to protection` | Check if intentional or needs public |
| CS0144 | `cannot construct abstract class` | Review class design |

## Runtime Errors (from test-result.json)

### File System — AUTO-FIX
| Exception | Pattern | Fix Action |
|-----------|---------|------------|
| FileNotFoundException | Missing config | Check `File.Exists()` before access |
| DirectoryNotFoundException | Wrong path | Verify path construction (Nomadic) |
| UnauthorizedAccessException | Permissions | Check access rights, use proper paths |

### Null Reference — AUTO-FIX with CAUTION
| Exception | Pattern | Fix Action |
|-----------|---------|------------|
| NullReferenceException | `Object ref not set` | Add null check, but analyze why null |
| ArgumentNullException | Null parameter | Add `?? throw` or `ArgumentNullException.ThrowIfNull` |

### Configuration — MANUAL REVIEW
| Exception | Pattern | Action |
|-----------|---------|--------|
| JsonException | Invalid JSON | Validate config format |
| KeyNotFoundException | Missing key | Check required vs optional keys |

### External/Network — REPORT
| Exception | Pattern | Action |
|-----------|---------|--------|
| HttpRequestException | Network error | External issue, verify connectivity |
| TimeoutException | Operation timeout | Check timeout values, server status |
| SocketException | Connection failed | Network issue, not code issue |

## Decision Matrix

```
Error detected
      ↓
Is it in AUTO-FIX list above?
   ├─ YES → Apply fix → Retest
   └─ NO → Can we understand the cause?
         ├─ YES → Attempt targeted fix → Retest
         └─ NO → Stop, report to user
```

## Fix Validation

After applying fix:
1. Must produce `git diff` (non-empty)
2. Must not introduce new warnings
3. Must pass build
4. Must not change public API (unless intentional)

## Stop Conditions

Stop auto-fix and report to user when:
- Same error repeats 3 times (stagnation)
- Fix produces no code change
- New error type appears (regression)
- Max 10 iterations reached
- Error involves external dependencies (network, auth, etc.)
