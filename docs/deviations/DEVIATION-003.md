# DEVIATION-003: WPF XAML Resource Loading in Hosted Mode

**Status:** ACTIVE  
**Created:** 2026-06-02  
**Deadline:** 2026-07-02 (30 days from creation)  
**Type:** Runtime Resource Resolution  
**Severity:** HIGH — GUI mode partially non-functional  

---

## Violation Description

Per INVARIANT_THEORY.md §2.2 (CQRS Separation), plugins should be independent and composable. However, WPF's XAML resource loading mechanism creates a coupling between the plugin and the host application's resource resolution context.

When `MinecraftLauncherGUIPlugin` runs inside an existing WPF Application (VantuzLauncher), WPF cannot resolve Pack URIs for XAML resources defined in the plugin assembly.

**Error:**
```
System.IO.IOException: Cannot locate resource 'mainwindow.xaml'.
URI: "/Vantuz.Products.MinecraftLauncher.GUI;component/mainwindow.xaml"
```

---

## Root Cause

WPF's `Application` class manages resource resolution through `ResourceManager` and `PackUriHelper`. When a plugin creates UI elements:

1. WPF constructs the Pack URI: `/AssemblyName;component/ResourcePath`
2. `Application.GetResourceStream(Uri)` attempts resolution
3. Resolution fails because the host Application's `ResourceAssembly` doesn't include plugin resources
4. Alternative: `AssemblyAssociatedContentFile` attribute isn't set for dynamic plugin loading

**Architectural conflict:**
- INVARIANT_THEORY.md §2.2 requires plugin independence
- WPF requires Application-level resource registration
- XAML compilation embeds resources as assembly resources, but WPF resolution is Application-scoped

---

## Impact Assessment

### Affected Functionality
- ✅ Plugin loads successfully (no assembly loading errors)
- ✅ STA thread creation works (DEVIATION-003-A resolution)
- ❌ XAML resource resolution fails (MainWindow cannot instantiate)
- ❌ GUI mode partially non-functional
- ⚠️ Credentials collection blocked (requires functional GUI window)

### Workaround Available
- Standalone mode: Plugin creates its own Application (works correctly)
- Hosted mode: Requires programmatic UI creation (no XAML)

---

## Temporary Resolution

### Implemented Fix (2026-06-02)
Added dual-mode initialization in `MinecraftLauncherGUIPlugin.cs`:

```csharp
// Check if we're running in an existing WPF Application context
if (Application.Current != null)
{
    // Hosted mode - use existing Application (XAML fails here)
    _app = Application.Current;
    // ... initialization continues but XAML loading fails
}
else
{
    // Standalone mode - create new STA thread with Application (works)
    var thread = new Thread(() => { ... });
    thread.SetApartmentState(ApartmentState.STA);
}
```

**Effect:** Plugin adapts to context but XAML resources unavailable in hosted mode.

---

## Permanent Resolution Plan

### Option A: Programmatic UI (Recommended)
Eliminate XAML dependency by creating UI elements in code:

**Advantages:**
- No WPF resource resolution dependency
- Full plugin independence per §2.2
- Works in both hosted and standalone modes

**Disadvantages:**
- Loss of XAML designer support
- More verbose UI code
- Requires rewriting MainWindow.xaml.cs

**Timeline:** 2-3 days implementation

### Option B: Custom Resource Manager
Implement `IResourceManager` for plugin-scoped resource resolution:

**Advantages:**
- Preserves XAML workflow
- Clean architectural separation

**Disadvantages:**
- Complex WPF internals manipulation
- Fragile (depends on WPF implementation details)
- May break with .NET updates

**Timeline:** 5-7 days research + implementation

### Option C: Pre-compiled BAML Loading
Load compiled BAML directly without Application resource resolution:

**Advantages:**
- Fast runtime loading
- No Application coupling

**Disadvantages:**
- Requires reflection on WPF internals
- Bypasses standard WPF initialization
- High maintenance cost

**Timeline:** 3-4 days + ongoing maintenance

---

## Decision

**Selected Option:** A (Programmatic UI)

**Rationale:**
- Aligns with INVARIANT_THEORY.md §2.2 (plugin independence)
- Simplest implementation
- Most maintainable long-term
- No fragile WPF internals dependencies

**Deadline:** 2026-07-02

**Rollback Condition:** If Option A requires >3 days, evaluate Option B.

---

## Verification

### Build-Time (Automated)
- ✅ ARM011: ComponentScope analyzer passes
- ✅ ARM012/013: Context key analyzers pass
- ✅ ARM-BUILD-XXX: All file presence checks pass

### Runtime (Manual)
- ⚠️ Hosted mode: XAML loading fails (this deviation)
- ✅ Standalone mode: Functional (not used in current architecture)

---

## References

- INVARIANT_THEORY.md §2.2: CQRS Separation
- INVARIANT_THEORY.md §2.1: Explicitness (XAML loading must be explicit)
- DEVIATION-001: Component Scope (related architectural context)
- WPF Pack URIs: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/app-development/pack-uris-in-wpf

---

## Changelog

| Date | Action | By |
|------|--------|-----|
| 2026-06-02 | Created | Agent |
| 2026-06-02 | Implemented dual-mode initialization | Agent |

---

*Deviation active until permanent resolution implemented.*
