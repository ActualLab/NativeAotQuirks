namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        const string typeName = "NativeAotQuirks.UnreferencedType, NativeAotQuirks";

        var instance = M((object)new UnreferencedType());
        Console.WriteLine($"=== Metadata_LiveCtorReference ===");
        Console.WriteLine($"Live `new UnreferencedType()`, result: {instance}\n");

        var type = Type.GetType(M(typeName))!;
        Console.WriteLine($"  Type.GetType: {type}");

        Test("ctor()", () => Activator.CreateInstance(type)!);
        PrintMembers(type);
    }
}
