; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARM006 | Architecture | Error | Pipeline Cycle Detection
ARM011 | Architecture | Error | Component Scope Invariant Violation
ARM012 | Contracts | Warning | Unmatched Context Key
ARM013 | Contracts | Warning | Similar Context Keys Detected

### Changed Rules

None

### Removed Rules

None

### Notes

ARM011: Detects Level 4 (Product) components implementing Level 2 (Plugin) interfaces.
Per INVARIANT_THEORY.md §2.3, Level N may only depend on Level N-1.

ARM012: Detects context.Set calls without corresponding context.Get.
Per INVARIANT_THEORY.md §2.1, all contracts must be explicit and verifiable.

ARM013: Detects similar context keys with different separators (underscore vs dot).
Prevents typos like gui_credential_provider vs gui.credential_provider.
