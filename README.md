# NativeAOT Quirks

A test project for experimenting with NativeAOT code generation, trimming, and type retention. Contains `CodeKeeper` — a reusable utility for forcing NativeAOT to preserve types and their members.

## CodeKeeper

`CodeKeeper` provides two methods to force NativeAOT to retain native code and metadata for types that would otherwise be trimmed:

### `Keep<T>()` — for accessible types

Uses `[DynamicallyAccessedMembers(All)]` on the type parameter to tell the trimmer to preserve all members of `T`.

```csharp
CodeKeeper.Keep<MyClass>();
CodeKeeper.Keep<RareClass<SomeType>>();
```

### `Keep(string)` — for internal/inaccessible types

Uses `Type.GetType(literal)` in a dead branch to force ILC to generate native code. However, `Type.GetType` alone generates code but does **not** preserve member metadata — the trimmer strips constructors, properties, methods, etc. To preserve members, the result must be followed by `.GetMembers(...)` calls covering all binding flag combinations:

```csharp
if (AlwaysTrue) return;
var t = Type.GetType(assemblyQualifiedTypeName);
t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
```

The string must be a compile-time literal at the **call site** — ILC analyzes it statically. The `[DynamicallyAccessedMembers(All)]` attribute on the parameter also helps the trimmer propagate metadata requirements.

```csharp
CodeKeeper.Keep("Namespace.InternalType`1[[ArgType, ArgAssembly]], TargetAssembly");
```

### `AlwaysFalse` / `AlwaysTrue`

Public fields used for dead branches that ILC can't eliminate:

```csharp
public static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 2;
public static readonly bool AlwaysTrue = !AlwaysFalse;
```

Since these are evaluated at runtime, ILC must assume either branch is reachable and generate code for both.

## Findings

### Type retention mechanisms

| Mechanism | Generates native code? | Preserves members? |
|---|---|---|
| `Type.GetType("literal")` alone | Yes | **No** — trimmer strips constructors/members |
| `Type.GetType("literal").GetConstructors()` | Yes | Constructors only |
| `Type.GetType("literal").GetMembers(...)` | Yes | Yes — must cover all binding flag combos |
| `[DynamicallyAccessedMembers(All)]` on generic `T` | Yes | Yes |
| `[DynamicDependency(All, typeof(T))]` | Yes | Yes |
| `[DynamicDependency(All, "TypeName", "Assembly")]` | **No** | Yes (metadata only) |
| `Assembly.GetType("literal")` | **No** | **No** |
| `Activator.CreateInstance(type)` in dead branch | No additional effect | **No** — trimmer can't trace runtime `Type` |
| `[DynamicallyAccessedMembers]` on string param | No additional effect | **No** — annotation doesn't apply to strings |

### Generic sharing (Universal Shared Generics)

Both class and struct generics share code for reference type arguments via `__Canon`. Neither shares code between different value type arguments — each struct `T` needs its own explicit retention.

| Scenario | What's kept | What works at runtime |
|---|---|---|
| `Keep<Foo<object>>()` | `Foo<object>` | `Foo<AnyRefType>` — all ref types share via `__Canon` |
| `Keep<Foo<StructA>>()` | `Foo<StructA>` | `Foo<StructA>` only — `Foo<StructB>` fails |
| `Keep<Foo<object>>()` + `Keep<Foo<StructA>>()` | Both | All ref types + `StructA` only — `StructB` still fails |

This applies equally to `class Foo<T>` and `struct Foo<T>` — the sharing rules are the same.

**Correction from earlier assumptions:** the original hypothesis that "one struct instantiation covers all structs for class generics" was disproven experimentally. Each distinct value type argument requires explicit retention regardless of whether the generic is a class or struct.

### Interface retention

#### What does `Keep<IFoo<T>>()` preserve?

For any interface method, there are effectively two entry points: `ConcreteType.Method` (the method as a member of the concrete type) and `ConcreteType.IFoo.Method` (the interface dispatch slot / vtable entry). These are backed by the same native code, but they have separate metadata and separate dispatch paths.

`Keep<IFoo<object>>()` preserves:
- The **interface dispatch implementation** (`ConcreteType.IFoo.Method`) — the vtable entries that make interface calls work
- The **interface type's reflection metadata** — `typeof(IFoo<T>).GetProperty("Value")` returns a valid `PropertyInfo`

It does NOT preserve:
- The **concrete type's own reflection metadata** — `concreteInstance.GetType().GetProperty("Value")` returns null
- The **concrete type's constructors** — `Activator.CreateInstance` fails with `MissingMethodException`

#### Dispatch code vs reflection metadata

| What you do | Preserves dispatch code? | Preserves reflection metadata? |
|---|---|---|
| `Keep<IRare<object>>()` | Yes | Yes (on interface type only) |
| Direct interface call: `((IRare<T>)impl).Value` | Yes | **No** |
| `new RareImpl<T>()` (direct ctor) | Ctor only | **No** |
| `Keep<RareImpl<object>>()` | Yes | Yes (on concrete type) |

Key insight: direct interface usage in code (`((IRare<T>)impl).Value = ...`) preserves the native code for dispatch but does **not** preserve reflection metadata — even on the interface type itself. Only `Keep<IRare<T>>()` (via `[DynamicallyAccessedMembers]`) preserves reflection metadata.

#### Interface generic sharing

| What's kept | `IRare<ClassA>` via reflection | `IRare<StructA>` via reflection |
|---|---|---|
| `Keep<IRare<object>>()` | **OK** — `__Canon` sharing | **OK** — also covered |
| `Keep<IRare<ClassBase>>()` | **OK** — `__Canon` sharing | **OK** — also covered |
| Nothing (just `new RareImpl<T>()`) | **NOT FOUND** | **NOT FOUND** |

Keeping an interface with any reference type argument (e.g., `object`, `ClassBase`) preserves interface dispatch and reflection metadata for **all** type arguments — including value types. This is different from direct type retention where value types are never shared. Interface dispatch uses a different mechanism that covers all type args.

#### Cross-implementor sharing

| Scenario | Result |
|---|---|
| Keep `IRare<object>`, test `RareImpl<object>` ctor via reflection | **FAIL** — `MissingMethodException` (ctor trimmed) |
| Keep `IRare<object>`, test `AnotherRareImpl<object>` ctor via reflection | **FAIL** — `MissingMethodException` (ctor trimmed) |
| Keep `RareImpl<object>`, test `AnotherRareImpl<object>` | **FAIL** — keeping one implementor does not cover others |

To call interface members via reflection: resolve the `MethodInfo`/`PropertyInfo` from the **interface type**, then invoke it on the concrete instance. Resolving from the concrete type will fail (NOT FOUND) because the concrete type's metadata was trimmed.

### `IlcTrimMetadata` — native code vs reflection metadata

NativeAOT treats code generation and metadata as **completely independent** concerns. Having native code for a member does NOT automatically preserve its reflection metadata (and vice versa).

`<IlcTrimMetadata>true</IlcTrimMetadata>` is the default. When enabled, the trimmer strips reflection metadata from all members that aren't explicitly marked for retention — even if those members have native code. For example, `new MyType()` compiles to a direct `newobj` IL instruction that generates native code for the ctor, but the trimmer doesn't consider that a reason to keep the ctor's reflection metadata. So `Activator.CreateInstance(type)` will fail with `MissingMethodException` even though the ctor code exists and was just called.

`<IlcTrimMetadata>false</IlcTrimMetadata>` preserves reflection metadata for every member that has native code. But it cannot preserve metadata for members with no code — if a method was never referenced and ILC didn't generate code for it, there's nothing to attach metadata to.

| Scenario: `new UnreferencedType()` (live call) | `IlcTrimMetadata=true` | `IlcTrimMetadata=false` |
|---|---|---|
| `Type.GetType` | Found | Found |
| `Activator.CreateInstance` (ctor) | **FAIL** — metadata stripped | OK |
| `GetProperty("Name")` (has code via initializer) | **NOT FOUND** | Found |
| `GetMethod("Compute")` (never called) | NOT FOUND | NOT FOUND |

| Scenario: no references at all | `IlcTrimMetadata=true` | `IlcTrimMetadata=false` |
|---|---|---|
| `Type.GetType` | null — type eliminated entirely | null — type eliminated entirely |

Key takeaways:
- `IlcTrimMetadata` only affects types that have native code. Unreferenced types are eliminated entirely regardless of this setting.
- With `IlcTrimMetadata=true`, you must use `CodeKeeper.Keep`, `[DynamicallyAccessedMembers]`, or `GetMembers(...)` to explicitly preserve metadata. Direct usage (`new`, method calls) generates code but not metadata.
- With `IlcTrimMetadata=false`, metadata is automatically preserved for any member that has code. This is a blunt instrument — it increases binary size but avoids the need for explicit metadata retention.
- `CodeKeeper.Keep` works correctly regardless of this setting — it provides targeted, per-type control over both code generation and metadata retention.

### Error diagnostics

| Error | Meaning |
|---|---|
| `NotSupportedException: 'Type' is missing native code` | ILC didn't generate native code for this type at all |
| `MissingMethodException: No parameterless constructor` | Native code exists (possibly via `__Canon` sharing) but the trimmer stripped constructor metadata |

The error type is the key diagnostic — `NotSupportedException` means you need to force code generation (e.g., `Type.GetType` literal or `Keep<T>`), while `MissingMethodException` means you need to preserve metadata (e.g., `.GetMembers(...)` or `[DynamicallyAccessedMembers]`).

## Writing Clean Tests

NativeAOT tests are tricky because ILC performs whole-program analysis. Any type reference anywhere in the compiled assembly can cause ILC to generate code for it, contaminating the test. Clean tests must ensure that **only** the `Keep(...)` / `CodeKeeper.Keep<T>()` call signals type retention — nothing else in the test should give ILC hints.

### Isolation rules

1. **Run one test at a time.** Edit `Program.cs` to call a single test method. Even commented-out code in other test methods is compiled into the assembly — if another test calls `Keep<RareClass<ClassA>>()`, ILC sees it and generates code, contaminating your test.

2. **Never reference the type under test directly.** Use `ActivateGeneric(typeof(RareClass<>), typeof(ClassA))` instead of `new RareClass<ClassA>()`. The open generic `typeof(RareClass<>)` doesn't trigger codegen for any closed instantiation — only the `Keep(...)` call should do that.

3. **Use `M()` to obscure values from ILC.** The helper `M<T>(T value)` passes the value through an array round-trip that ILC can't analyze statically. Use it to prevent ILC from tracing type information:
   ```csharp
   // ILC can trace this:
   var type = Type.GetType("MyType, MyAssembly");
   // ILC cannot trace this:
   var type = Type.GetType(M("MyType, MyAssembly"));
   ```

4. **Cast to `object` before passing to helpers.** When you must construct a type directly (e.g., to test interface dispatch), cast to `object` via `M()` so ILC can't see the concrete type flowing into member-testing helpers:
   ```csharp
   var impl = M((object)new RareImpl<object>());  // ILC sees ctor, but not member usage
   TestMember(impl, "Value");  // ILC sees TestMember(object, string), not the concrete type
   ```

5. **Test interface members via `TestInterfaceMember`.** Instead of casting to the interface and calling members directly (which gives ILC a direct reference), resolve the member from the interface `Type` via reflection and invoke it on the instance:
   ```csharp
   // Bad — ILC sees the interface call and preserves the member:
   ((IRare<object>)impl).Value = "test";

   // Good — only the Keep(...) call determines what's preserved:
   TestInterfaceMember(impl, typeof(IRare<object>), "Value");
   ```

### Test helpers

| Helper | Purpose |
|---|---|
| `M<T>(value)` | Obscure a value from ILC's static analysis |
| `ActivateGeneric(openType, typeArgs...)` | Create a closed generic instance via `MakeGenericType` + `Activator.CreateInstance` |
| `Activate(type, ctorArgTypes...)` | Find and invoke a constructor (public or private) via reflection |
| `TestMember(instanceOrType, name)` | Exercise a field/property/method on a concrete type via reflection |
| `TestMembers(instanceOrType, names...)` | Batch version of `TestMember` |
| `TestInterfaceMember(instance, interfaceType, name)` | Resolve a member from the interface type, invoke on the instance |
| `TestInterfaceMembers(instance, interfaceType, names...)` | Batch version of `TestInterfaceMember` |
| `Test(label, action)` | Run an action, print OK/FAIL with exception details |

## Dependency Graph Analysis

`IlcGenerateDgmlFile` is enabled in the project. After publishing, ILC generates a `.dgml.xml` file in `obj/Release/net10.0/win-x64/native/` that contains the full dependency graph — every type, method, and reason it was included. Open it in Visual Studio or parse it to understand why a specific type or member was kept or trimmed.

Useful for debugging unexpected behavior, e.g., "why does this type have code but no ctor metadata?" — search for the type in the DGML to see what referenced it and through which dependency path.

## Building and Running

```cmd
PublishAndRun.cmd
```

Or manually:

```bash
PATH="/c/Program Files (x86)/Microsoft Visual Studio/Installer:$PATH" dotnet publish -c Release
bin\Release\net10.0\win-x64\publish\NativeAotQuirks.exe
```

Edit `Program.cs` to select which test to run — only one test should be active at a time to avoid cross-contamination of type retention between tests.
