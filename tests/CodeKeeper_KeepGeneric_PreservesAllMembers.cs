namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        const string typeName = "NativeAotQuirks.KitchenSink, NativeAotQuirks";
        CodeKeeper.Keep<KitchenSink>();

        Console.WriteLine("=== CodeKeeper_KeepGeneric_PreservesAllMembers ===\n");
        TestKitchenSinkMembers(typeName);
    }
}
