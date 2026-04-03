namespace NativeAotQuirks;

public static partial class Tests
{
    public static void Run()
    {
        new CodeKeeperAdvanced();
        CodeKeeper.KeepReturnTypes<StructA, StructB, StructC>();

        Console.WriteLine("=== KeepReturnTypes_ThreeStructs ===");
        Console.WriteLine("Kept: KeepReturnTypes<StructA, StructB, StructC>\n");

        var resultOpen = M(typeof(ActualLab.Result<>));
        var taskOpen = M(typeof(Task<>));
        var vtaskOpen = M(typeof(ValueTask<>));

        foreach (var (name, argType) in new[] {
            ("StructA", typeof(StructA)),
            ("StructB", typeof(StructB)),
            ("StructC", typeof(StructC))
        })
        {
            var t = M(argType);
            var resultT = resultOpen.MakeGenericType(t);
            var taskT = taskOpen.MakeGenericType(t);
            var taskResultT = taskOpen.MakeGenericType(resultT);
            var vtaskT = vtaskOpen.MakeGenericType(t);
            var vtaskResultT = vtaskOpen.MakeGenericType(resultT);

            Console.WriteLine($"  --- {name} ---");
            Test($"{name}",                () => Activator.CreateInstance(t)!);
            Test($"Result<{name}>",        () => Activator.CreateInstance(resultT)!);
            Test($"Task<{name}>",          () => { Console.Write($"type: {taskT} "); return taskT; });
            Test($"Task<Result<{name}>>",  () => { Console.Write($"type: {taskResultT} "); return taskResultT; });
            Test($"ValueTask<{name}>",     () => Activator.CreateInstance(vtaskT)!);
            Test($"VTask<Result<{name}>>", () => Activator.CreateInstance(vtaskResultT)!);
            Console.WriteLine();
        }
    }
}
