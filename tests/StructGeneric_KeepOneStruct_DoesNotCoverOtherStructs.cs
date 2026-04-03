namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Keep("NativeAotQuirks.RareStruct`1[[NativeAotQuirks.StructA, NativeAotQuirks]], NativeAotQuirks");

        Console.WriteLine("=== StructGeneric_KeepOneStruct_DoesNotCoverOtherStructs ===");
        Console.WriteLine("Kept: RareStruct<StructA>\n");
        Test("RareStruct<StructA>", () => ActivateGeneric(typeof(RareStruct<>), typeof(StructA)));
        Test("RareStruct<StructB>", () => ActivateGeneric(typeof(RareStruct<>), typeof(StructB)));
    }
}
