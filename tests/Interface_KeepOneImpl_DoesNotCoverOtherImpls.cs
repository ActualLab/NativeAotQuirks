namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Keep<RareImpl<object>>();

        Console.WriteLine("=== Interface_KeepOneImpl_DoesNotCoverOtherImpls ===");
        Console.WriteLine("Kept: RareImpl<object>\n");
        Test("RareImpl<object>",        () => ActivateGeneric(typeof(RareImpl<>), typeof(object)));
        Test("AnotherRareImpl<object>", () => ActivateGeneric(typeof(AnotherRareImpl<>), typeof(object)));
    }
}
