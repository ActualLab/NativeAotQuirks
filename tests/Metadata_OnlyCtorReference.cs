namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        const string typeName = "NativeAotQuirks.UnreferencedType, NativeAotQuirks";

        if (CodeKeeper.AlwaysFalse)
            _ = new UnreferencedType();

        Console.WriteLine("=== Metadata_OnlyCtorReference ===");
        Console.WriteLine("Only `new UnreferencedType()` in dead branch.\n");

        var type = Type.GetType(M(typeName));
        Console.WriteLine($"  Type.GetType: {(type != null ? type.ToString() : "null")}");
        if (type == null) { Console.WriteLine("  Type not found."); return; }

        Test("ctor()", () => Activator.CreateInstance(type)!);
        PrintMembers(type);
    }
}
