namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        CodeKeeper.Keep<IRare<object>>();

        Console.WriteLine("=== Interface_KeepInterface_DoesNotCoverImplementors ===");
        Console.WriteLine("Kept: IRare<object>\n");
        Test("RareImpl<object>",        () => ActivateGeneric(typeof(RareImpl<>), typeof(object)));
        Test("AnotherRareImpl<object>", () => ActivateGeneric(typeof(AnotherRareImpl<>), typeof(object)));
    }
}
