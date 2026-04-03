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

| Scenario | Implementor ctor (via reflection) | Concrete member (via reflection) | Interface member (via reflection) |
|---|---|---|---|
| Keep `IRare<object>` only | **FAIL** — `MissingMethodException` | **NOT FOUND** — trimmed | **OK** — preserved on interface type |
| Keep `IRare<object>` + direct `new RareImpl<object>()` | OK (ILC sees ctor) | **NOT FOUND** — trimmed | **OK** — invoke via interface `MethodInfo` |
| Keep `RareImpl<object>` | OK | OK | OK |
| Keep `RareImpl<object>`, test `AnotherRareImpl<object>` | **FAIL** — `MissingMethodException` | N/A | N/A |

Key findings:
- Keeping an interface preserves the **interface dispatch slots** (native code for the interface method implementations) and the **interface's reflection metadata**, but does NOT preserve constructors or reflection metadata on concrete implementations.
- To call interface members via reflection on a kept interface: resolve the `MethodInfo`/`PropertyInfo` from the **interface type**, then invoke it on the concrete instance.
- Keeping one implementor does NOT cover other implementors of the same interface.

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
