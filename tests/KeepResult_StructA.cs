namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        new CodeKeeperAdvanced();
        CodeKeeper.KeepResult<StructA>();

        Console.WriteLine("=== KeepResult_StructA ===");
        Console.WriteLine("Kept: Result<StructA>\n");

        var resultOpen = M(typeof(ActualLab.Result<>));
        Test("Result<object>",    () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(object))))!);
        Test("Result<ClassA>",    () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(ClassA))))!);
        Test("Result<ClassB>",    () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(ClassB))))!);
        Test("Result<StructA>",   () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(StructA))))!);
        Test("Result<StructB>",   () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(StructB))))!);
    }
}
