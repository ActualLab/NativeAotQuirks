namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        // Use default CodeKeeperImpl — KeepResult is an empty stub there
        CodeKeeper.Instance = new CodeKeeperImpl();
        CodeKeeper.KeepResult<StructA>();

        Console.WriteLine("=== KeepResult_StructA_NoAdvanced ===");
        Console.WriteLine("Kept: KeepResult<StructA> via CodeKeeperImpl (no advanced override)\n");

        var resultOpen = M(typeof(ActualLab.Result<>));
        Test("Result<object>",    () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(object))))!);
        Test("Result<ClassA>",    () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(ClassA))))!);
        Test("Result<StructA>",   () => Activator.CreateInstance(resultOpen.MakeGenericType(M(typeof(StructA))))!);
    }
}
