using Microsoft.CodeAnalysis;

namespace Armatura.Core.Sdk;

/// <summary>
/// Diagnostic descriptors for Armatura architectural constitution analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    // ARM005: CQRS Blender Detection
    public static readonly DiagnosticDescriptor CqrsBlender = new(
        id: "ARM005",
        title: "CQRS Blender Detected",
        messageFormat: "ARM005: Component '{0}' contains both Query and Command operations. Split into separate Query and Command classes per Armatura architectural constitution.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CQRS Separation Invariant violation: No component may contain both Read (Query) and Write (Command) operations."
    );

    // ARM007: QuantizedNode Inheritance Required
    public static readonly DiagnosticDescriptor MissingQuantizedNode = new(
        id: "ARM007",
        title: "Missing QuantizedNode Inheritance",
        messageFormat: "ARM007: Plugin '{0}' must inherit from QuantizedNode and override ExecuteQuantumAsync(). Free-form async Task methods are compile-time forbidden per Armatura architectural constitution.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Quantized Execution Invariant violation: All plugins must inherit from QuantizedNode and use ExecuteQuantumAsync() instead of free-form async Task methods."
    );

    // ARM008: Free-form Async Method Detection
    public static readonly DiagnosticDescriptor FreeFormAsync = new(
        id: "ARM008",
        title: "Free-form Async Method Forbidden",
        messageFormat: "ARM008: Method '{0}' uses free-form async pattern. Use QuantizedNode.ExecuteQuantumAsync() with cooperative yielding instead.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Free-form async Task methods are forbidden. All async operations must be within ExecuteQuantumAsync() quantum boundaries."
    );

    // ARM009: Runtime DI Container Forbidden
    public static readonly DiagnosticDescriptor RuntimeDIContainer = new(
        id: "ARM009",
        title: "Runtime DI Container Forbidden",
        messageFormat: "ARM009: Method '{0}' creates runtime DI service provider. Compile-time dependency resolution required per Armatura architectural constitution.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Runtime DI container creation is forbidden. All dependencies must be resolved at compile-time with explicit constructor injection."
    );

    // ARM010: Plugin-side Resource Cleanup Forbidden
    public static readonly DiagnosticDescriptor PluginResourceCleanup = new(
        id: "ARM010",
        title: "Plugin-side Resource Cleanup Forbidden",
        messageFormat: "ARM010: {0} - Resource cleanup must be host-managed through DAG Ref Counting",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Plugin-side resource cleanup is forbidden. The host must manage all resource lifecycles through DAG Ref Counting."
    );

    // ARM011: Component Scope Invariant Violation
    public static readonly DiagnosticDescriptor ComponentScopeViolation = new(
        id: "ARM011",
        title: "Component Scope Invariant Violation",
        messageFormat: "ARM011: Level {0} component '{1}' implements Level {2} interface '{3}'. Components may only implement interfaces from their own level or one level above per INVARIANT_THEORY.md §2.3.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Component Scope Invariant violation: Level N components may only implement interfaces from Level N or N-1."
    );

    // ARM012: Unmatched Context Key
    public static readonly DiagnosticDescriptor UnmatchedContextKey = new(
        id: "ARM012",
        title: "Unmatched Context Key",
        messageFormat: "ARM012: Context key '{0}' is used without a matching Set/Get pair. All context keys must have corresponding producers and consumers.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Context key contract violation: context.Set/Get pairs must be matched across the compilation."
    );

    // ARM013: Similar Context Keys Detected
    public static readonly DiagnosticDescriptor SimilarContextKeys = new(
        id: "ARM013",
        title: "Similar Context Keys Detected",
        messageFormat: "ARM013: Context keys '{0}' and '{1}' are similar but use different separators. Normalize to a single separator convention.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Context key inconsistency: similar keys with different separators detected (e.g., gui_credential_provider vs gui.credential_provider)."
    );
}
