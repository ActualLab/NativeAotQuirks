namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Keep<IRare<object>>();

        Console.WriteLine("=== Interface_KeepInterfaceOfObject_CoversAllTypeArgs ===");
        Console.WriteLine("Kept: IRare<object>. Test RareImpl<ClassA> and RareImpl<StructA> via interface.\n");

        // Resolve IRare<ClassA> and IRare<StructA> via reflection to avoid ldtoken contamination
        var iRareOpen = M(typeof(IRare<>));
        var iRareClassA = iRareOpen.MakeGenericType(M(typeof(ClassA)));
        var iRareStructA = iRareOpen.MakeGenericType(M(typeof(StructA)));

        var implClassA = M((object)new RareImpl<ClassA>());
        Console.WriteLine($"  new RareImpl<ClassA>(): OK ({implClassA})");
        Console.WriteLine("  --- RareImpl<ClassA> via IRare<ClassA> ---");
        TestInterfaceMember(implClassA, iRareClassA, "Value");

        var implStructA = M((object)new RareImpl<StructA>());
        Console.WriteLine($"  new RareImpl<StructA>(): OK ({implStructA})");
        Console.WriteLine("  --- RareImpl<StructA> via IRare<StructA> ---");
        TestInterfaceMember(implStructA, iRareStructA, "Value");
    }
}
