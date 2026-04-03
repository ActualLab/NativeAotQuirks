using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NativeAotQuirks;

public static partial class Tests
{
    static void TestKitchenSinkMembers(string typeName)
    {
        var type = Type.GetType(M(typeName))!;
        Console.WriteLine($"  Type.GetType: {type}");

        Test("ctor(string)", () => Activate(type, typeof(string)));

        var instance = Activate(type, typeof(string));
        Console.WriteLine($"  Created: {instance}\n");

        Console.WriteLine("  --- Instance fields ---");
        TestMembers(instance, "Name", "_counter");

        Console.WriteLine("  --- Static fields ---");
        TestMembers(type, "InstanceCount");

        Console.WriteLine("  --- Instance properties ---");
        TestMembers(instance, "Counter", "Tag");

        Console.WriteLine("  --- Static properties ---");
        TestMembers(type, "Description");

        Console.WriteLine("  --- Instance methods ---");
        TestMembers(instance, "Greet", "Increment", "Format");

        Console.WriteLine("  --- Static methods ---");
        TestMembers(type, "StaticMethod");
    }

    static void PrintMembers(Type type)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
        var nameProp = type.GetProperty("Name", flags);
        Console.WriteLine($"  GetProperty(\"Name\"): {(nameProp != null ? "found" : "NOT FOUND")}");
        var computeMethod = type.GetMethod("Compute", flags);
        Console.WriteLine($"  GetMethod(\"Compute\"): {(computeMethod != null ? "found" : "NOT FOUND")}");
        var allMembers = type.GetMembers(flags);
        Console.WriteLine($"  All members: {string.Join(", ", allMembers.Select(m => m.Name))}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void WrappedKeep<T>()
    {
        CodeKeeper.Keep<T>();
    }
}
