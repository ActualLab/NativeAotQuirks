namespace NativeAotQuirks;

public class RareClass<T> { public T? Value; }
public struct RareStruct<T> { public T? Value; }

public class ClassBase { public override string ToString() => "ClassBase"; }
public class ClassA : ClassBase { public override string ToString() => "ClassA"; }
public class ClassB : ClassBase { public override string ToString() => "ClassB"; }
public struct StructA { public int X; public override string ToString() => $"StructA({X})"; }
public struct StructB { public string? Y; public override string ToString() => $"StructB({Y})"; }

// Interface + implementation generics for sharing tests
public interface IRare<T> { T? Value { get; set; } }
public class RareImpl<T> : IRare<T> { public T? Value { get; set; } }
public class AnotherRareImpl<T> : IRare<T> { public T? Value { get; set; } }

// Unreferenced type — never used directly in any test code
public class UnreferencedType
{
    public string? Name { get; set; }
    public int Compute(int x = 5) => x * 2;
    public override string ToString() => $"UnreferencedType({Name})";
}

// Type with all kinds of members for comprehensive Keep(string) testing
public class KitchenSink
{
    public KitchenSink() { Name = "default"; }
    private KitchenSink(string name) { Name = name; }

    // Fields
    public string Name;
    private int _counter;
    public static int InstanceCount;

    // Properties
    public int Counter { get => _counter; set => _counter = value; }
    private string Tag { get; set; } = "";
    public static string Description { get; set; } = "default";

    // Methods
    public string Greet() => $"Hello from {Name}";
    private int Increment() => ++_counter;
    public string Format(string prefix = ">>") => $"{prefix} {Name}({_counter})";
    public static string StaticMethod() => "static-result";

    public override string ToString() => $"KitchenSink({Name})";
}
