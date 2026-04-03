namespace NativeAotQuirks;

public static class Tests
{
    public static void CodeKeeper_KeepString_PreservesAllMembers()
    {
        const string typeName = "NativeAotQuirks.KitchenSink, NativeAotQuirks";
        CodeKeeper.Keep(typeName);

        Console.WriteLine("=== CodeKeeper_KeepString_PreservesAllMembers ===\n");
        TestKitchenSinkMembers(typeName);
    }

    public static void CodeKeeper_KeepGeneric_PreservesAllMembers()
    {
        const string typeName = "NativeAotQuirks.KitchenSink, NativeAotQuirks";
        CodeKeeper.Keep<KitchenSink>();

        Console.WriteLine("=== CodeKeeper_KeepGeneric_PreservesAllMembers ===\n");
        TestKitchenSinkMembers(typeName);
    }

    public static void Test_GenericSharing()
    {
        // --- Keep ---
        CodeKeeper.Keep<RareClass<object>>();
        CodeKeeper.Keep("NativeAotQuirks.RareStruct`1[[System.Object, System.Private.CoreLib]], NativeAotQuirks");
        CodeKeeper.Keep<RareClass<StructA>>();
        CodeKeeper.Keep("NativeAotQuirks.RareStruct`1[[NativeAotQuirks.StructA, NativeAotQuirks]], NativeAotQuirks");

        // --- Test ---
        Console.WriteLine("=== Test_GenericSharing: Generic Sharing ===\n");

        Console.WriteLine("--- RareClass<T> (class generic) ---");
        Console.WriteLine("Retained: <object> and <StructA>");
        Test("RareClass<ClassA>",  () => ActivateGeneric(typeof(RareClass<>), typeof(ClassA)));
        Test("RareClass<ClassB>",  () => ActivateGeneric(typeof(RareClass<>), typeof(ClassB)));
        Test("RareClass<StructA>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructA)));
        Test("RareClass<StructB>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructB)));

        Console.WriteLine("\n--- RareStruct<T> (struct generic) ---");
        Console.WriteLine("Retained: <object> and <StructA>");
        Test("RareStruct<ClassA>",  () => ActivateGeneric(typeof(RareStruct<>), typeof(ClassA)));
        Test("RareStruct<ClassB>",  () => ActivateGeneric(typeof(RareStruct<>), typeof(ClassB)));
        Test("RareStruct<StructA>", () => ActivateGeneric(typeof(RareStruct<>), typeof(StructA)));
        Test("RareStruct<StructB>", () => ActivateGeneric(typeof(RareStruct<>), typeof(StructB)));
    }

    // Private methods

    static void TestKitchenSinkMembers(string typeName)
    {
        // Get type at runtime via obscured string so ILC can't trace it
        typeName = M(typeName);
        var type = Type.GetType(typeName)!;
        Console.WriteLine($"  Type.GetType: {type}");

        // Private ctor(string)
        Test("ctor(string)", () => Activate(type, typeof(string)));

        // Create instance for member tests
        var instance = Activate(type, typeof(string));
        Console.WriteLine($"  Created: {instance}\n");

        // Instance members
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
}
