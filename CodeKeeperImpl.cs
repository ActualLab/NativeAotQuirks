using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NativeAotQuirks;

public class CodeKeeperImpl
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        if (CodeKeeper.AlwaysTrue) return;

        var type = typeof(T);
        type.GetConstructors();
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void Keep(Type type)
    {
        if (CodeKeeper.AlwaysTrue) return;

        type.GetConstructors();
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void Keep(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName)
    {
        if (CodeKeeper.AlwaysTrue) return;

        var type = Type.GetType(assemblyQualifiedTypeName);
        type.GetConstructors();
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void KeepResult<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void KeepReturnType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void KeepReturnTypes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>()
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public virtual void KeepReturnTypes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>()
    { }
}