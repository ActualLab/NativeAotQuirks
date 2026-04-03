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

    // --- Class generic sharing ---

    // Keeping <object> covers all reference type args via __Canon
    public static void ClassGeneric_KeepObject_CoversAllRefTypes()
    {
        CodeKeeper.Keep<RareClass<object>>();

        Console.WriteLine("=== ClassGeneric_KeepObject_CoversAllRefTypes ===");
        Console.WriteLine("Kept: RareClass<object>\n");
        Test("RareClass<ClassA>", () => ActivateGeneric(typeof(RareClass<>), typeof(ClassA)));
        Test("RareClass<ClassB>", () => ActivateGeneric(typeof(RareClass<>), typeof(ClassB)));
    }

    // Keeping one struct instantiation does NOT cover other structs
    public static void ClassGeneric_KeepOneStruct_DoesNotCoverOtherStructs()
    {
        CodeKeeper.Keep<RareClass<StructA>>();

        Console.WriteLine("=== ClassGeneric_KeepOneStruct_DoesNotCoverOtherStructs ===");
        Console.WriteLine("Kept: RareClass<StructA>\n");
        Test("RareClass<StructA>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructA)));
        Test("RareClass<StructB>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructB)));
    }

    // Keeping <object> + one struct still does NOT cover other structs
    public static void ClassGeneric_KeepObjectAndOneStruct_DoesNotCoverOtherStructs()
    {
        CodeKeeper.Keep<RareClass<object>>();
        CodeKeeper.Keep<RareClass<StructA>>();

        Console.WriteLine("=== ClassGeneric_KeepObjectAndOneStruct_DoesNotCoverOtherStructs ===");
        Console.WriteLine("Kept: RareClass<object> + RareClass<StructA>\n");
        Test("RareClass<ClassA>",  () => ActivateGeneric(typeof(RareClass<>), typeof(ClassA)));
        Test("RareClass<ClassB>",  () => ActivateGeneric(typeof(RareClass<>), typeof(ClassB)));
        Test("RareClass<StructA>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructA)));
        Test("RareClass<StructB>", () => ActivateGeneric(typeof(RareClass<>), typeof(StructB)));
    }

    // --- Struct generic sharing ---

    // Keeping <object> covers all ref types via __Canon (same as class generics)
    public static void StructGeneric_KeepObject_CoversAllRefTypes()
    {
        CodeKeeper.Keep("NativeAotQuirks.RareStruct`1[[System.Object, System.Private.CoreLib]], NativeAotQuirks");

        Console.WriteLine("=== StructGeneric_KeepObject_CoversAllRefTypes ===");
        Console.WriteLine("Kept: RareStruct<object>\n");
        Test("RareStruct<object>", () => ActivateGeneric(typeof(RareStruct<>), typeof(object)));
        Test("RareStruct<ClassA>", () => ActivateGeneric(typeof(RareStruct<>), typeof(ClassA)));
        Test("RareStruct<ClassB>", () => ActivateGeneric(typeof(RareStruct<>), typeof(ClassB)));
    }

    // Keeping one struct does NOT cover other structs (no sharing for struct generics)
    public static void StructGeneric_KeepOneStruct_DoesNotCoverOtherStructs()
    {
        CodeKeeper.Keep("NativeAotQuirks.RareStruct`1[[NativeAotQuirks.StructA, NativeAotQuirks]], NativeAotQuirks");

        Console.WriteLine("=== StructGeneric_KeepOneStruct_DoesNotCoverOtherStructs ===");
        Console.WriteLine("Kept: RareStruct<StructA>\n");
        Test("RareStruct<StructA>", () => ActivateGeneric(typeof(RareStruct<>), typeof(StructA)));
        Test("RareStruct<StructB>", () => ActivateGeneric(typeof(RareStruct<>), typeof(StructB)));
    }

    // --- Interface → implementor sharing ---

    // Does keeping IRare<object> also keep RareImpl<object>?
    public static void Interface_KeepInterface_DoesNotCoverImplementors()
    {
        CodeKeeper.Keep<IRare<object>>();

        Console.WriteLine("=== Interface_KeepInterface_DoesNotCoverImplementors ===");
        Console.WriteLine("Kept: IRare<object>\n");
        Test("RareImpl<object>",        () => ActivateGeneric(typeof(RareImpl<>), typeof(object)));
        Test("AnotherRareImpl<object>", () => ActivateGeneric(typeof(AnotherRareImpl<>), typeof(object)));
    }

    // Keep interface + construct impl directly, test interface member via reflection only
    public static void Interface_KeepInterface_PreservesInterfaceMembers()
    {
        CodeKeeper.Keep<IRare<object>>();

        Console.WriteLine("=== Interface_KeepInterface_PreservesInterfaceMembers ===");
        Console.WriteLine("Kept: IRare<object>. Construct impl directly, test Value via reflection.\n");

        // Direct ctor call — ILC sees this and generates ctor code
        var impl = M((object)new RareImpl<object>());
        Console.WriteLine($"  new RareImpl<object>(): OK ({impl})");

        // Test via concrete type reflection
        Console.WriteLine("  --- On concrete type ---");
        TestMember(impl, "Value");

        // Test via interface type reflection (resolves member on interface, invokes on instance)
        Console.WriteLine("  --- Via interface ---");
        TestInterfaceMember(impl, typeof(IRare<object>), "Value");
    }

    // Does keeping one implementor cover another implementor of the same interface?
    public static void Interface_KeepOneImpl_DoesNotCoverOtherImpls()
    {
        CodeKeeper.Keep<RareImpl<object>>();

        Console.WriteLine("=== Interface_KeepOneImpl_DoesNotCoverOtherImpls ===");
        Console.WriteLine("Kept: RareImpl<object>\n");
        Test("RareImpl<object>",        () => ActivateGeneric(typeof(RareImpl<>), typeof(object)));
        Test("AnotherRareImpl<object>", () => ActivateGeneric(typeof(AnotherRareImpl<>), typeof(object)));
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
