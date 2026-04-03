using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NativeAotQuirks;

public static class CodeKeeper
{
    public static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 2;
    public static readonly bool AlwaysTrue = !AlwaysFalse;

    public static CodeKeeperImpl Instance { get; set; } = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => Instance.Keep<T>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep(Type type)
        => Instance.Keep(type);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName)
        => Instance.Keep(assemblyQualifiedTypeName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepResult<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => Instance.KeepResult<T>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepReturnType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => Instance.KeepReturnType<T>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepReturnTypes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>()
        => Instance.KeepReturnTypes<T1, T2>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepReturnTypes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>()
        => Instance.KeepReturnTypes<T1, T2, T3>();
}
