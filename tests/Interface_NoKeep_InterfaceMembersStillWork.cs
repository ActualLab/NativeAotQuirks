namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        Console.WriteLine("=== Interface_NoKeep_InterfaceMembersStillWork ===");
        Console.WriteLine("Kept: nothing. Direct interface cast for ClassA, no Keep.\n");

        var iRareOpen = M(typeof(IRare<>));
        var iRareClassA = iRareOpen.MakeGenericType(M(typeof(ClassA)));
        var iRareStructA = iRareOpen.MakeGenericType(M(typeof(StructA)));

        var implClassA = M((object)new RareImpl<ClassA>());
        Console.WriteLine($"  new RareImpl<ClassA>(): OK ({implClassA})");
        // Direct interface cast — ILC sees this, but does it preserve reflection metadata?
        var intClassA = (IRare<ClassA>)implClassA;
        intClassA.Value = CodeKeeper.AlwaysTrue ? intClassA.Value : default;
        Console.WriteLine("  --- RareImpl<ClassA> via IRare<ClassA> ---");
        TestInterfaceMember(implClassA, iRareClassA, "Value");

        var implStructA = M((object)new RareImpl<StructA>());
        Console.WriteLine($"  new RareImpl<StructA>(): OK ({implStructA})");
        Console.WriteLine("  --- RareImpl<StructA> via IRare<StructA> ---");
        TestInterfaceMember(implStructA, iRareStructA, "Value");
    }
}
