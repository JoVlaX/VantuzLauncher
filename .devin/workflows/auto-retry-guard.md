---
description: Termination guarantee and stagnation detection
---

# Auto-Retry Guard Protocol

Система защиты от бесконечных циклов и гарантия завершения (Termination per INVARIANT_THEORY 2.1).

## Core Guarantees

```
DAG of fix attempts:
    State: (ErrorType, Location, Iteration)
    MaxDepth: 10
    No cycles allowed
```

## Limits Configuration

```yaml
max_iterations: 10
stagnation_threshold: 3  # Same error repeated
regression_penalty: true   # New error type = stop
min_code_change: 1       # Bytes in diff required
```

## Stagnation Detection

### Pattern Recognition
```
Iteration 1: CS0246 in HeadlessRunner.cs line 45
Iteration 2: CS0246 in HeadlessRunner.cs line 45
Iteration 3: CS0246 in HeadlessRunner.cs line 45
→ STAGNATION DETECTED → STOP
```

### Implementation
```powershell
$currentError = Get-CurrentError
$history = Get-FixHistory

if ($history[-3..-1] | Where { $_ -eq $currentError }) {
    Stop-AutoFix -Reason "Stagnation: same error 3 times"
}
```

## Progress Tracking

### State File: `auto-fix-state.json`
```json
{
  "runId": "uuid",
  "startTime": "2026-01-01T00:00:00Z",
  "iterations": 3,
  "errorsSeen": ["CS0246", "CS1002"],
  "lastError": "CS1002",
  "fixesApplied": 2,
  "status": "in_progress"
}
```

### History File: `auto-fix-history.log`
```
[2026-01-01 00:00:01] Iteration 1: Build failed, CS0246, Fix: add using
[2026-01-01 00:00:03] Iteration 2: Build failed, CS1002, Fix: add semicolon
[2026-01-01 00:00:05] Iteration 3: Build success, Test failed, FileNotFound
```

## Termination Conditions

### Normal Termination
- Build + Test success → Exit 0
- Max iterations (10) reached → Exit 1, report last error

### Early Termination (Safety)
- Stagnation detected (3× same error) → Exit 2
- Regression (new error type) → Exit 3
- No code change produced → Exit 4
- External error (network, etc.) → Exit 5

## Recovery Protocol

When guard stops execution:

1. **Preserve state**: Keep `auto-fix-state.json`
2. **Generate report**: Include full history + last error
3. **Suggest manual steps**: Based on error classification
4. **Reset on new session**: Clear state when user provides new work

## User Override

User can override guard:
```
/force-continue  # Continue despite stagnation
/reset-guard     # Clear state and retry
/ignore-error    # Mark error as expected
```

## Verification

Guard guarantees (per INVARIANT_THEORY):
- ✅ Deterministic: Same input → same termination
- ✅ Bounded: Max 10 iterations
- ✅ Measurable: State file tracks progress
- ✅ Verifiable: History log for audit

## Integration

This guard is called by `auto-fix-orchestrator.ps1` before each retry:
```powershell
$guard = Test-RetryGuard
if (-not $guard.CanContinue) {
    Stop-AutoFix -Reason $guard.StopReason
}
```
