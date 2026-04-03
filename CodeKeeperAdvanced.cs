using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ActualLab;

namespace NativeAotQuirks;

public class CodeKeeperAdvanced : CodeKeeperImpl
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void KeepResult<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        Keep<T>();
        Keep<Result<T>>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void KeepReturnType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        KeepResult<T>();
        Keep<Task<T>>();
        Keep<Task<Result<T>>>();
        Keep<ValueTask<T>>();
        Keep<ValueTask<Result<T>>>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void KeepReturnTypes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>()
    {
        KeepReturnType<T1>();
        KeepReturnType<T2>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void KeepReturnTypes<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>()
    {
        KeepReturnTypes<T1, T2>();
        KeepReturnType<T3>();
    }
}