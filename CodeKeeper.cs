using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NativeAotQuirks;

public static class CodeKeeper
{
    public static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 2;
    public static readonly bool AlwaysTrue = !AlwaysFalse;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName)
    {
        if (AlwaysTrue) return;
        var t = Type.GetType(assemblyQualifiedTypeName);
        // t.GetConstructors();
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }
}
