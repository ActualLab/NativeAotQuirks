using System.Runtime.CompilerServices;

namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        const string typeName = "System.Runtime.CompilerServices.StrongBox`1[[System.Int32, System.Private.CoreLib]], System.Private.CoreLib";
        WrappedKeep<StrongBox<int>>();

        Console.WriteLine("=== CodeKeeper_WrappedKeep_ExternalType ===");
        Console.WriteLine("WrappedKeep<T> (no annotation) with StrongBox<int>\n");

        var type = Type.GetType(M(typeName));
        Console.WriteLine($"  Type.GetType: {(type != null ? type.ToString() : "null")}");
        if (type == null) { Console.WriteLine("  Type not found."); return; }

        Test("ctor()", () => Activator.CreateInstance(type)!);
        PrintMembers(type);
    }
}
