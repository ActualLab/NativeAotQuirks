namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        const string typeName = "NativeAotQuirks.KitchenSink, NativeAotQuirks";
        WrappedKeep<KitchenSink>();

        Console.WriteLine("=== CodeKeeper_WrappedKeep_PreservesAllMembers ===");
        Console.WriteLine("WrappedKeep<T> (no annotation) — expect failure.\n");

        var type = Type.GetType(M(typeName));
        Console.WriteLine($"  Type.GetType: {(type != null ? type.ToString() : "null")}");
        if (type == null) { Console.WriteLine("  Type not found — annotation chain broken."); return; }

        Test("ctor()", () => Activator.CreateInstance(type)!);
        PrintMembers(type);
    }
}
