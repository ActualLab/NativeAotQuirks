namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        CodeKeeper.Instance = new CodeKeeperBase();
        CodeKeeper.Keep("NativeAotQuirks.RareStruct`1[[System.Object, System.Private.CoreLib]], NativeAotQuirks");

        Console.WriteLine("=== StructGeneric_KeepObject_CoversAllRefTypes ===");
        Console.WriteLine("Kept: RareStruct<object>\n");
        Test("RareStruct<object>", () => ActivateGeneric(typeof(RareStruct<>), typeof(object)));
        Test("RareStruct<ClassA>", () => ActivateGeneric(typeof(RareStruct<>), typeof(ClassA)));
        Test("RareStruct<ClassB>", () => ActivateGeneric(typeof(RareStruct<>), typeof(ClassB)));
    }
}
