// ICredentialProvider now defined in Vantuz.Core for cross-plugin compatibility
// Per INVARIANT_THEORY.md §1.2: Shared types in Core for AssemblyLoadContext isolation
// This file provides type aliases for backward compatibility during migration

using Vantuz.Core;

namespace Vantuz.Products.MinecraftLauncher.GUI;

// Type aliases for backward compatibility - these reference Vantuz.Core types
using Credentials = global::Vantuz.Core.Credentials;
using ICredentialProvider = global::Vantuz.Core.ICredentialProvider;
using CredentialsSubmittedEventArgs = global::Vantuz.Core.CredentialsSubmittedEventArgs;
