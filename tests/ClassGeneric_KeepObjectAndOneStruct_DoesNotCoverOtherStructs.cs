namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Keep<RareClass<object>>();
        CodeKeeper.Keep<RareClass<StructA>>();

        Console.WriteLine("=== ClassGeneric_KeepObjectAndOneStruct_DoesNotCoverOtherStructs ===");
        Console.WriteLine("Kept: RareClass<object> + RareClass<StructA>\n");
        Test("RareClass<ClassA>",  () => ActivateGeneric(typeof(RareClass<>), typeof(ClassA)));
        Test("RareClass<ClassB>",  () => ActivateGeneric(typeof(RareClass<>), typeof(ClassB)));
        Test("RareClass<StructA>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructA)));
        Test("RareClass<StructB>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructB)));
    }
}
