namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Keep<IRare<object>>();

        Console.WriteLine("=== Interface_KeepInterface_PreservesInterfaceMembers ===");
        Console.WriteLine("Kept: IRare<object>. Construct impl directly, test Value via reflection.\n");

        var impl = M((object)new RareImpl<object>());
        Console.WriteLine($"  new RareImpl<object>(): OK ({impl})");

        var iRareObject = M(typeof(IRare<>)).MakeGenericType(M(typeof(object)));

        Console.WriteLine("  --- On concrete type ---");
        TestMember(impl, "Value");

        Console.WriteLine("  --- Via interface ---");
        TestInterfaceMember(impl, iRareObject, "Value");
    }
}
