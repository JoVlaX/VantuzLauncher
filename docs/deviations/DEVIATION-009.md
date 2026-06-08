---
version: 1.0
parent: INVARIANT_THEORY.md
parent_version: 1.1
---

# Deviation Protocol 009: CQRS Violation in ExternalAbstraction (MinecraftGameProvider)

**Status:** Resolved 2026-06-08  
**Created:** 2026-06-07T21:15:00+05:00  
**Deadline:** 2026-08-07T23:59:59+05:00  
**Closed:** 2026-06-08T20:20:00+05:00  
**Owner:** Agent Cascade  

---

## Violation Summary

| Aspect | Details |
|--------|---------|
| **Rule Violated** | INVARIANT_THEORY.md §2.2 CQRS Separation Invariant |
| **Location** | `Vantuz.Plugins.Minecraft/MinecraftGameProvider.cs` (deleted) → `MinecraftGameQueryProvider.cs` + `MinecraftGameCommandProvider.cs` |
| **Nature** | ExternalAbstraction implemented both Query and Command in single class; resolved by splitting into two classes |

## INVARIANT_THEORY §2.2 Text

> No component may contain both Read (Query) and Write (Command) operations.
> `Invariant: ∀c ∈ C: R(c) ≠ ∅ ⟹ W(c) = ∅` (Query)
> `∀c ∈ C: W(c) ≠ ∅ ⟹ R(c) = ∅` (Command)

## Evidence

```
File: Vantuz.Plugins.Minecraft/MinecraftGameProvider.cs
Lines 26–224: CheckVersionAsync (Query) — reads file system, returns VersionCheckResult
Lines 71–224: InstallVersionAsync (Command) — writes files, downloads artifacts, returns InstallResult
Lines 226–292: BuildLaunchParametersAsync (Query) — reads installed artifacts, returns LaunchParameters
```

`R(MinecraftGameProvider) = {CheckVersionAsync, BuildLaunchParametersAsync}`  
`W(MinecraftGameProvider) = {InstallVersionAsync}`  
`R(c) ∩ W(c) ≠ ∅` → **VIOLATION**

## Root Cause

The `IGameProvider` contract (defined in `Vantuz.Core/IGameProvider.cs`) requires all three operations in a single interface:

```csharp
public interface IGameProvider
{
    string ProviderName { get; }
    Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct);
    Task<InstallResult> InstallVersionAsync(string version, string installDir, IStatusReporter reporter, CancellationToken ct, TimeSpan? timeout = null);
    Task<LaunchParameters> BuildLaunchParametersAsync(string version, string installDir, SessionContext context, IStatusReporter reporter, CancellationToken ct);
}
```

Per INVARIANT_THEORY §2.3 Component Scope Invariant:
```
Scope(ARM007) = {Plugin, ExternalAbstraction}
```
ARM007 applies to ExternalAbstractions. However, §2.2 CQRS applies to ALL components (`∀c ∈ C`). There is no scope restriction that exempts ExternalAbstractions from CQRS.

## Justification for Deviation

1. **Interface contract constraint:** `IGameProvider` is consumed by `GameInstallerCommand` and `GameLaunchCommand`, which expect a single provider instance with all capabilities. Splitting would require interface redesign and pipeline changes.

2. **CmlLib isolation:** Per `Armatura:126`, `MinecraftGameProvider` is the ONLY component referencing CmlLib. Splitting it would require two CmlLib-dependent classes, increasing coupling surface area.

3. **Scope(ARM007) includes ExternalAbstraction:** While §2.2 is universal, ARM007 (QuantizedNode inheritance) is explicitly scoped to `{Plugin, ExternalAbstraction}`. This suggests ExternalAbstractions have pragmatic exceptions for external API bridges.

4. **No side-effect leakage:** `CheckVersionAsync` and `BuildLaunchParametersAsync` are pure reads; `InstallVersionAsync` is a pure write. The component does not interleave reads and writes within a single method.

## Resolution Applied

**Option A: Split Provider** — implemented 2026-06-08.

Split `MinecraftGameProvider` into:
- `MinecraftGameQueryProvider` (lines ~20–215) — `CheckVersionAsync`, `BuildLaunchParametersAsync`, shared helpers `VerifyForgeLibraries`, `IsForgeVersion`, `ParseForgeVersion`
- `MinecraftGameCommandProvider` (lines ~20–220) — `InstallVersionAsync`

Updated `IGameProvider` in `Vantuz.Core/Contracts.cs` to expose `IGameQueryProvider` and `IGameCommandProvider`.

Updated consumers:
- `GameInstallerCommand` → uses `IGameCommandProvider`
- `GameLaunchCommand` → uses `IGameQueryProvider`
- `MinecraftProviderCommand` → registers both facets separately

Deleted `MinecraftGameProvider.cs`.

Post-build verification (`ARM-BUILD-022`) passes with zero violations.

## Verification

| Claim | F_doc | E_doc |
|-------|-------|-------|
| "MinecraftGameQueryProvider contains only Query operations" | Any method writes to filesystem or mutates state | `grep -n 'public Task.*Async' MinecraftGameQueryProvider.cs` shows only `CheckVersionAsync`, `BuildLaunchParametersAsync` |
| "MinecraftGameCommandProvider contains only Command operations" | Any method reads from filesystem without mutation | `grep -n 'public Task.*Async' MinecraftGameCommandProvider.cs` shows only `InstallVersionAsync` |
| "IGameProvider is composed of IGameQueryProvider + IGameCommandProvider" | `IGameProvider` is a single monolithic interface | `grep -n 'interface IGameProvider' Vantuz.Core/Contracts.cs` shows composite of two facets |
| "Builder ARM-BUILD-022 reports zero CQRS violations" | Builder `verify-dir` reports CQRS violation for Minecraft plugin | `dotnet build` exits 0 with zero verification errors |

## Confidence Boundary

> **Lesson:** ExternalAbstractions bridging third-party libraries (CmlLib) may require unified interfaces when the external API itself couples queries and commands. The deviation MUST be contained: no pipeline plugin may violate CQRS. `MinecraftGameProvider` is the only ExternalAbstraction in the project; all pipeline plugins (`Vantuz.Plugins.*`) remain CQRS-compliant per ARM005.

---

## References

- INVARIANT_THEORY.md §2.2 CQRS Separation Invariant
- INVARIANT_THEORY.md §2.3 Component Scope Invariant
- `Vantuz.Plugins.Minecraft/MinecraftGameProvider.cs`
- `Vantuz.Core/IGameProvider.cs`
- Audit: `docs/audits/compliance-audit-2026-06-07-forge-library.md` (Violation #6)
