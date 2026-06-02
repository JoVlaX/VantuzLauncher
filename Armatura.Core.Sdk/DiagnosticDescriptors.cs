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
    // Per INVARIANT_THEORY.md §2.3: Level N may only depend on Level N-1
    public static readonly DiagnosticDescriptor ComponentScopeViolation = new(
        id: "ARM011",
        title: "Component Scope Invariant Violation",
        messageFormat: "ARM011: Level {0} component '{1}' implements Level {2} interface '{3}'. Per INVARIANT_THEORY.md §2.3, migrate to appropriate namespace.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Component Scope Invariant violation: Level N components may only implement interfaces from Level N or N-1."
    );

    // ARM012: Unmatched Context Key
    // Per INVARIANT_THEORY.md §2.1: All contracts must be explicit and verifiable
    public static readonly DiagnosticDescriptor UnmatchedContextKey = new(
        id: "ARM012",
        title: "Unmatched Context Key",
        messageFormat: "ARM012: Context key '{0}' has no matching {1}. Ensure all context.Set calls have corresponding context.Get.",
        category: "Contracts",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Context key contract violation: All context.Set calls must have corresponding context.Get within the same pipeline scope."
    );

    // ARM013: Similar Context Keys Detected
    // Detects potential key mismatches like gui_credential_provider vs gui.credential_provider
    public static readonly DiagnosticDescriptor SimilarContextKeys = new(
        id: "ARM013",
        title: "Similar Context Keys Detected",
        messageFormat: "ARM013: Context keys '{0}' and '{1}' are similar but differ in separator (underscore vs dot). This may indicate a typo.",
        category: "Contracts",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Potential key mismatch: Similar keys with different separators detected. Use consistent naming convention."
    );
}
