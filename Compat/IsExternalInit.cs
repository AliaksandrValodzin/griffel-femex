#if NETSTANDARD2_0

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// The marker the compiler emits into the signature of every <c>init</c>
    /// accessor. It ships in the framework from .NET 5, and not at all in
    /// netstandard2.0 — so without this declaration the netstandard leg cannot
    /// compile <c>Geometry/Vector3d.cs</c>'s <c>readonly record struct</c>, whose
    /// positional members are init-only, nor <c>Interop/</c>'s request types.
    ///
    /// Declaring it here is the documented way to close that gap: the type is
    /// pure metadata, the compiler only ever looks it up by name, and the net8.0
    /// leg gets the real one from the framework instead. It is deliberately not
    /// public — two assemblies each declaring a public one is an ambiguity for
    /// anything referencing both.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}

#endif
