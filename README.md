# NativeAOT Code Generation: What We Learned

## The Core Problem

NativeAOT compiles all code ahead of time. When framework code creates closed generic types at runtime via `MakeGenericType` + `Activator.CreateInstance`, the AOT compiler never sees these types statically, so it doesn't generate native code for them. At runtime, this fails with:

```
NotSupportedException: 'SomeType`1[T]' is missing native code or metadata.
```

vs. when code IS present but constructor metadata was trimmed:

```
MissingMethodException: No parameterless constructor defined for type 'SomeType`1[T]'.
```

The error type is the key diagnostic: `NotSupportedException` = no native code, `MissingMethodException` = code exists but trimmer stripped the constructor.

## How to Force Code Generation

### `Type.GetType("...", assembly)` with string literals

ILC (the NativeAOT IL Compiler) analyzes `Type.GetType` string literals at compile time and generates native code for the types they reference. This is the primary mechanism we found for forcing code generation of internal/inaccessible closed generic types.

```csharp
// ILC sees this literal and generates native code for the closed generic
Type.GetType("Some.Internal.Converter`1[[MyType, MyAssembly]], TargetAssembly");
```

**Important:** only `Type.GetType` works — `Assembly.GetType` does NOT trigger code generation.

**Important:** the string must be a compile-time literal. If you build it at runtime (e.g., via `string.Concat`), ILC can't analyze it.

### `new T()` / direct usage in code

Direct usage of a type in code (even behind an always-false branch) forces code generation:

```csharp
static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 10;

if (AlwaysFalse)
{
    _ = new MyClass<SomeType>();  // forces codegen
}
```

This works for class generics but NOT for struct generics — `new SomeStruct<T>()` is just zero-initialization and doesn't generate constructor/method code.

### What does NOT work

| Mechanism | Preserves metadata? | Generates native code? |
|---|---|---|
| `[DynamicDependency(All, typeof(ClosedGeneric))]` | Yes | Yes (only with `typeof()`, not string) |
| `[DynamicDependency(All, "TypeName", "Assembly")]` | Yes | **No** |
| `rd.xml` with `Dynamic="Required All"` | Yes | **No** |
| `Assembly.GetType("literal string")` | Depends on trimming | **No** |
| `[DynamicallyAccessedMembers]` on constraints | Preserves members | Yes (for constructors/members of the annotated type) |

`[DynamicDependency]` with a `typeof()` reference works because `typeof()` emits a type token in IL that ILC treats as a direct reference. The string overload only affects trimming, not code generation.

## Generic Sharing Rules

NativeAOT uses Universal Shared Generics (USG) for reference types. The sharing behavior differs between class and struct generics:

### Class generics (`class Foo<T>`)

| What you retain | What works at runtime |
|---|---|
| `Foo<object>` | `Foo<AnyReferenceType>` — all ref types share code via `__Canon` |
| `Foo<OneStructType>` | `Foo<AnyStructType>` — one struct instantiation provides a template for all |
| `Foo<object>` + `Foo<AnyOneStruct>` | Everything — full coverage |

Key finding: for class generics, retaining a single value-type instantiation provides a template that covers ALL value types. This is surprising but confirmed experimentally.

### Struct generics (`struct Bar<T>`)

| What you retain | What works at runtime |
|---|---|
| `Bar<object>` | **Only** `Bar<object>` — no sharing with other ref types |
| `Bar<StructA>` | **Only** `Bar<StructA>` — no sharing with other structs |
| `Bar<StructA>` + `Bar<StructB>` | Only those two specific instantiations |

Struct generics have **no sharing at all**. Every distinct type argument needs its own explicitly retained instantiation. This includes reference type arguments — `Bar<object>` does NOT cover `Bar<SomeClass>`.

### Why struct generics can't share

Struct generics embed the layout of `T` directly. The runtime needs exact method tables and field offsets for each instantiation. Class generics store references (pointers) regardless of `T`, enabling code sharing.

## Controlling Constructor Preservation

Even when native code is generated, the trimmer may strip constructor metadata needed by `Activator.CreateInstance`. Solutions:

### `[DynamicallyAccessedMembers(All)]` on generic constraints

```csharp
static void KeepComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    where T : new()
{
    if (AlwaysFalse)
        _ = new T();
}
```

### Direct `new` in always-false branch

For class generics, `new MyClass<T>()` in a dead branch preserves both native code AND the constructor.

## Practical Pattern: CodeKeeper

Combine all techniques in a single class:

```csharp
public static class CodeKeeper
{
    static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 10;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep()
    {
        // Preserve Blazor component constructors
        KeepComponent<MyComponent>();

        if (AlwaysFalse)
        {
            // Force codegen for internal closed generics via Type.GetType literals
            Type.GetType("Namespace.InternalType`1[[ArgType, ArgAssembly]], TargetAssembly");

            // Force codegen for accessible types via direct usage
            _ = new MyClass<object>();      // covers all ref type args
            _ = new MyClass<SomeStruct>();  // covers all struct args (for class generics)
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void KeepComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        where T : new()
    {
        if (AlwaysFalse)
            _ = new T();
    }
}
```

Call `CodeKeeper.Keep()` early in app startup.

## `AlwaysFalse` Pattern

The compiler must believe the branch is reachable for it to analyze string literals and generate code. Use:

```csharp
static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 10;
```

This is evaluated at runtime, so the compiler can't prove it's false. Alternatives like `volatile bool` also work but are less clean. A simple `const bool` or `if (false)` would be dead-code eliminated.

## Discovering Missing Types

Add a first-chance exception handler to log all failures:

```csharp
AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
{
    Log($"FIRST-CHANCE: {args.Exception}");
};
```

Then grep for `"is missing native code"` in the log. The type names in the error messages are the exact types you need to add to `CodeKeeper`.

## Additional MSBuild Properties

For MAUI Blazor apps with NativeAOT:

```xml
<PropertyGroup Condition="windows">
    <PublishAot>true</PublishAot>
    <!-- BlazorWebView IPC uses reflection-based JSON serialization -->
    <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

## Publishing

`vswhere.exe` must be on PATH for the native linker to find MSVC tools:

```bash
PATH="/c/Program Files (x86)/Microsoft Visual Studio/Installer:$PATH" dotnet publish -c Release
```
