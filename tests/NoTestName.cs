namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        Console.WriteLine("No test selected. Build with -p:TestName=<name> or run via:");
        Console.WriteLine("  Run.cmd <TestName>");
        Console.WriteLine();
        Console.WriteLine("Available tests:");
        foreach (var file in Directory.GetFiles("tests", "*.cs").Order())
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith("_") && name != "NoTestName")
                Console.WriteLine($"  {name}");
        }
    }
}
