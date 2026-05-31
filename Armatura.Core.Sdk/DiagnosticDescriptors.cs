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
}
