namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        const string typeName = "NativeAotQuirks.KitchenSink, NativeAotQuirks";
        CodeKeeper.Keep(typeName);

        Console.WriteLine("=== CodeKeeper_KeepString_PreservesAllMembers ===\n");
        TestKitchenSinkMembers(typeName);
    }
}
