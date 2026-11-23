namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// The marker type the C# 9 compiler needs for <c>init</c> accessors. Unity's .NET Standard 2.1 profile does not ship
    /// it, so the assembly declares its own (batch 14 step 3: the shared TraitDatabase rows are set once through object
    /// initializers and are read-only afterwards).
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
