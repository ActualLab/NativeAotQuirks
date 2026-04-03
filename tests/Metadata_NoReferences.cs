namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        const string typeName = "NativeAotQuirks.UnreferencedType, NativeAotQuirks";

        Console.WriteLine("=== Metadata_NoReferences ===");
        Console.WriteLine("No references at all. Just Type.GetType(M(...)).\n");

        var type = Type.GetType(M(typeName));
        Console.WriteLine($"  Type.GetType: {(type != null ? type.ToString() : "null")}");
        if (type == null) { Console.WriteLine("  Type not found."); return; }

        Test("ctor()", () => Activator.CreateInstance(type)!);
        PrintMembers(type);
    }
}
