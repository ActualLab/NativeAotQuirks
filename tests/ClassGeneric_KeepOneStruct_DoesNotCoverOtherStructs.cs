namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        CodeKeeper.Keep<RareClass<StructA>>();

        Console.WriteLine("=== ClassGeneric_KeepOneStruct_DoesNotCoverOtherStructs ===");
        Console.WriteLine("Kept: RareClass<StructA>\n");
        Test("RareClass<StructA>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructA)));
        Test("RareClass<StructB>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructB)));
    }
}
