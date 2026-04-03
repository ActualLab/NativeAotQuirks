namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Keep<RareClass<object>>();

        Console.WriteLine("=== ClassGeneric_KeepObject_CoversAllRefTypes ===");
        Console.WriteLine("Kept: RareClass<object>\n");
        Test("RareClass<ClassA>", () => ActivateGeneric(typeof(RareClass<>), typeof(ClassA)));
        Test("RareClass<ClassB>", () => ActivateGeneric(typeof(RareClass<>), typeof(ClassB)));
    }
}
