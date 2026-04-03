# NativeAOT Quirks

A test project for experimenting with NativeAOT code generation, trimming, and type retention. Contains `CodeKeeper` — a reusable utility for forcing NativeAOT to preserve types and their members.

## CodeKeeper

`CodeKeeper` provides two methods to force NativeAOT to retain native code and metadata for types that would otherwise be trimmed:

### `Keep<T>()` — for accessible types

Uses two complementary mechanisms:
1. `[DynamicallyAccessedMembers(All)]` on `T` — tells the trimmer to preserve reflection metadata
2. `typeof(T).GetConstructors()` + `typeof(T).GetMembers(...)` in a dead branch — forces ILC to generate native code

Both are necessary. The annotation alone preserves metadata but does **not** force code generation for struct generics with value type arguments (e.g., `Result<StructA>`). The `typeof(T).GetMembers(...)` calls in the dead branch force ILC to see the concrete type and generate code for it.

```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
public static void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
{
    if (AlwaysTrue) return;
    var t = typeof(T);
    t.GetConstructors();
    t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
    t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
}
```

```csharp
CodeKeeper.Keep<MyClass>();
CodeKeeper.Keep<RareClass<SomeType>>();
```

### `Keep(string)` — for internal/inaccessible types

Uses `[DynamicallyAccessedMembers(All)]` on the string parameter plus `Type.GetType(literal).GetMembers(...)` in a dead branch. The annotation tells the trimmer to preserve metadata; the `Type.GetType` + `GetMembers` calls force code generation.

```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
public static void Keep(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName)
{
    if (AlwaysTrue) return;
    var t = Type.GetType(assemblyQualifiedTypeName);
    t.GetConstructors();
    t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
    t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
}
```

The string must be a compile-time literal at the **call site** — ILC's dataflow analysis traces the value statically. If the string passes through an intermediary method parameter, the chain breaks and nothing is preserved.

```csharp
// Works — literal flows directly to [DynamicallyAccessedMembers] parameter:
CodeKeeper.Keep("Namespace.InternalType`1[[ArgType, ArgAssembly]], TargetAssembly");

// Does NOT work — intermediary method breaks the dataflow chain:
void Wrapper(string s) => CodeKeeper.Keep(s);
Wrapper("Namespace.InternalType`1[[ArgType, ArgAssembly]], TargetAssembly");
```

A wrapper method works only if its own parameter also carries `[DynamicallyAccessedMembers(All)]`:

```csharp
// Works — annotation propagates through the wrapper:
void Wrapper([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string s)
    => CodeKeeper.Keep(s);
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
| `[DynamicallyAccessedMembers(All)]` on generic `T` (annotation only, empty body) | Ref types via `__Canon`: yes. **Struct with value-type arg: NO** | Yes |
| `[DynamicallyAccessedMembers(All)]` + `typeof(T).GetMembers(...)` in dead branch | Yes (all types) | Yes |
| `[DynamicallyAccessedMembers(All)]` on `string` param (with literal at call site) | Yes | Yes — the trimmer parses the type name from the literal |
| `[DynamicDependency(All, typeof(T))]` | Yes | Yes |
| `[DynamicDependency(All, "TypeName", "Assembly")]` | **No** | Yes (metadata only) |
| `Type.GetType("literal")` alone | Yes (code only) | **No** — trimmer strips constructors/members |
| `Type.GetType("literal").GetConstructors()` | Yes | Constructors only |
| `Type.GetType("literal").GetMembers(...)` | Yes | Yes — must cover all binding flag combos |
| `Assembly.GetType("literal")` | **No** | **No** |
| `Activator.CreateInstance(type)` in dead branch | No additional effect | **No** — trimmer can't trace runtime `Type` |
| Wrapping `Keep` without forwarding annotations | **No** | **No** — dataflow chain breaks (IL2091 warning) |

### Annotation propagation across method boundaries

The trimmer uses a **contract-based** system at method boundaries. When method `A<T>()` calls method `B<[DynamicallyAccessedMembers(All)] T>()`, the trimmer checks whether `A`'s `T` satisfies `B`'s requirement. If it doesn't, the trimmer emits IL2091 and **treats the call as contributing nothing** to `T`'s member preservation — regardless of what `B`'s body does.

```csharp
// Broken chain — WrappedKeep's T has no annotation, so Keep<T>() is treated as a no-op:
static void WrappedKeep<T>()                        // T has no annotation
    => CodeKeeper.Keep<T>();                         // Keep requires [DAM(All)] — IL2091 warning

// Working chain — annotation forwarded:
static void WrappedKeep<[DynamicallyAccessedMembers(All)] T>()
    => CodeKeeper.Keep<T>();                         // T satisfies requirement — works
```

This applies equally to `string` and `Type` parameters. The same rules hold:
- Each method boundary enforces annotation matching independently
- A missing annotation at any link in the chain breaks propagation at that point
- The trimmer does not look inside the called method to discover requirements — it only checks the declared annotations on the target's parameters

DGML confirms this: when `WrappedKeep<T>()` lacks the annotation, its "Dataflow analysis" node has **zero outgoing edges** — the trimmer analyzed it but produced nothing. The `Keep<KitchenSink>()` call exists as compiled code but generates no `Reflectable type/method` entries.

### Composing `Keep` for complex type patterns

Since annotations propagate correctly across method boundaries (as long as every link carries `[DynamicallyAccessedMembers(All)]`), you can build higher-level retention helpers that compose `Keep<T>()` calls:

```csharp
// Keep T and all its common return type wrappers
public static void KeepReturnType<[DynamicallyAccessedMembers(All)] T>()
{
    Keep<T>();
    Keep<Result<T>>();
    Keep<Task<T>>();
    Keep<Task<Result<T>>>();
    Keep<ValueTask<T>>();
    Keep<ValueTask<Result<T>>>();
}

// Keep multiple types at once
public static void KeepReturnTypes<
    [DynamicallyAccessedMembers(All)] T1,
    [DynamicallyAccessedMembers(All)] T2,
    [DynamicallyAccessedMembers(All)] T3>()
{
    KeepReturnTypes<T1, T2>();
    KeepReturnType<T3>();
}
```

This works because:
1. Each method in the chain has `[DynamicallyAccessedMembers(All)]` on its type parameters — the annotation chain is unbroken
2. `Keep<T>()` uses `typeof(T).GetMembers(...)` in a dead branch — this forces code generation even for nested struct generics like `ValueTask<Result<StructA>>`
3. The trimmer traces through the entire call graph: `KeepReturnTypes<StructA, StructB, StructC>()` → `KeepReturnType<StructA>()` → `Keep<Result<StructA>>()` → `typeof(Result<StructA>).GetMembers(...)`

Tested with `KeepReturnTypes<StructA, StructB, StructC>()` — all 18 combinations (3 types x 6 wrappers) produce native code and preserve metadata, including deeply nested struct generics like `ValueTask<Result<StructC>>`.

### Virtual dispatch and the `CodeKeeper.Instance` pattern

`CodeKeeper` is a static facade that delegates to `CodeKeeper.Instance` (a `CodeKeeperImpl`). The base class `CodeKeeperImpl` implements basic `Keep` methods with the `typeof(T).GetMembers(...)` dead-branch pattern, while `KeepResult`/`KeepReturnType`/`KeepReturnTypes` are empty stubs. `CodeKeeperAdvanced` overrides those stubs with real implementations that compose `Keep<T>()` calls.

```csharp
// In test setup:
CodeKeeper.Instance = new CodeKeeperAdvanced();  // enables KeepResult/KeepReturnType
CodeKeeper.KeepReturnTypes<StructA, StructB, StructC>();
```

Key findings:
- **Virtual dispatch preserves annotations.** `[DynamicallyAccessedMembers(All)]` on virtual method parameters propagates correctly through overrides — the trimmer analyzes the actual override body that ILC compiles.
- **The override must be instantiated.** If `CodeKeeperAdvanced` is never allocated (i.e., only `CodeKeeperImpl` is used), ILC never compiles the override bodies, and `KeepResult<T>()` calls the base empty stub — nothing is preserved. Tested: `KeepResult_StructA_NoAdvanced` confirms all `Result<T>` fail with `NotSupportedException` when using `CodeKeeperImpl` directly.
- **This enables modular code keeping.** A library can ship `CodeKeeperImpl` with basic `Keep` methods. An application can extend it with `CodeKeeperAdvanced` that knows about application-specific type patterns (e.g., `Result<T>`, `Task<Result<T>>`). The annotations propagate correctly through the inheritance chain.

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
| `Keep<IRare<object>>()` + `new RareImpl<ClassA>()` + `new RareImpl<StructA>()` | **OK** | **OK** |
| `Keep<IRare<ClassBase>>()` + `new RareImpl<ClassA>()` + `new RareImpl<StructA>()` | **OK** | **OK** |
| Just `new RareImpl<ClassA>()` + `new RareImpl<StructA>()` (no Keep) | **NOT FOUND** | **NOT FOUND** |

This works for value types too, but **not** via `__Canon` sharing. DGML analysis reveals the actual mechanism:

1. `Keep<IRare<object>>()` makes the `IRare<T>` interface **reflection-visible** (preserves `get_Value`/`set_Value` as reflectable methods on the open generic)
2. `new RareImpl<StructA>()` in the test generates a **constructed type** for `RareImpl<StructA>`
3. ILC sees that `RareImpl<StructA>` implements a reflection-visible interface (`IRare<T>`)
4. This triggers: "Interface definition was visible" → `IRare<StructA>` gets metadata automatically
5. The reflectable methods from step 1 become available on `IRare<StructA>`

So it's the combination of **"interface is reflection-visible"** (from `Keep`) + **"implementor is constructed"** (from `new` or other code reference) that produces the metadata for closed generic interface types. Without `Keep`, the interface isn't reflection-visible, so step 4 never triggers even though the implementor exists.

Important: the test must construct the implementor (e.g., `new RareImpl<StructA>()`) for this to work. `Keep<IRare<object>>()` alone does NOT generate `IRare<StructA>` — it only makes the interface reflection-visible. The closed generic is materialized when ILC encounters a constructed implementor.

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

1. **Run one test at a time.** Each test lives in its own file under `tests/`. The `.csproj` uses a `TestName` MSBuild property to include only one test file at compilation. Run via `Run.cmd <TestName>` or `dotnet publish -c Release -p:TestName=<TestName>`. This ensures no other test code is compiled into the assembly.

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

6. **Avoid `typeof()` for closed generic interfaces.** `typeof(IRare<StructA>)` emits a `ldtoken` that tells ILC about `IRare<StructA>` — contaminating interface sharing tests. Instead, construct closed generics via reflection:
   ```csharp
   // Bad — ldtoken introduces IRare<StructA> to ILC:
   TestInterfaceMember(impl, typeof(IRare<StructA>), "Value");

   // Good — open generic + MakeGenericType via M():
   var iRareStructA = M(typeof(IRare<>)).MakeGenericType(M(typeof(StructA)));
   TestInterfaceMember(impl, iRareStructA, "Value");
   ```
   DGML analysis confirmed this: `typeof(IRare<StructA>)` appears as a `ldtoken` edge from the test method, independently introducing the type to ILC regardless of any `Keep` call.

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

## NativeAOT MSBuild Properties

Key MSBuild properties that control code generation, trimming, and metadata retention:

### Metadata and reflection

| Property | Default | Description |
|---|---|---|
| `PublishAot` | `false` | Enable NativeAOT compilation |
| `IlcTrimMetadata` | `true` | Strip reflection metadata from members without explicit retention hints. When `true`, code generation and metadata are independent — having native code does NOT preserve metadata. When `false`, metadata is preserved for all members that have code. |
| `IlcGenerateCompleteTypeMetadata` | `true` | Generate complete type metadata for types that have code |
| `IlcDisableReflection` | `false` | Disable reflection entirely for maximum trimming |
| `JsonSerializerIsReflectionEnabledByDefault` | `false` | Allow System.Text.Json reflection-based serialization |

### Code generation and optimization

| Property | Default | Description |
|---|---|---|
| `IlcOptimizationPreference` | `Speed` | `Speed`, `Size`, or blank |
| `IlcFoldIdenticalMethodBodies` | `false` | Fold identical method bodies to reduce binary size |
| `IlcInstructionSet` | native | CPU instruction sets (e.g., `avx2,bmi2,lzcnt`) |

### Diagnostics

| Property | Default | Description |
|---|---|---|
| `IlcGenerateDgmlFile` | `false` | Generate dependency graph files for analyzing why types/methods were kept or trimmed |
| `IlcGenerateStackTraceData` | `true` | Include method names in stack traces |
| `SuppressTrimAnalysisWarnings` | `false` | Suppress IL20xx/IL30xx trimming warnings |

### Runtime behavior

| Property | Default | Description |
|---|---|---|
| `InvariantGlobalization` | `false` | Remove globalization data for smaller binary |
| `UseSystemResourceKeys` | `false` | Strip exception message strings |
| `EventSourceSupport` | `true` | EventSource/ETW support |

## Dependency Graph Analysis

`IlcGenerateDgmlFile` is enabled in the project. After publishing, ILC generates two DGML files in `obj/Release/net10.0/win-x64/native/`:
- `NativeAotQuirks.scan.dgml.xml` — the trimming/scanning dependency graph (what drives retention decisions)
- `NativeAotQuirks.codegen.dgml.xml` — the code generation dependency graph

These are large XML files (100K+ lines). Use `grep` to trace dependencies:

```bash
# Find nodes for a type:
grep "IRare.*StructA" *.scan.dgml.xml | grep "Node"

# Find what points TO a node (why was it kept?):
grep "Target=\"1401\"" *.scan.dgml.xml

# Find what a node points TO (what does it cause to be kept?):
grep "Source=\"671\"" *.scan.dgml.xml | grep "ldtoken"
```

Key node types to look for:
- `ldtoken` — a `typeof()` reference in code; introduces a type to ILC
- `newobj` — a `new` call; generates constructor code
- `Reflectable type/method` — metadata preserved by `[DynamicallyAccessedMembers]`
- `Dataflow analysis` — the trimmer's static analysis of annotation propagation
- `Interface definition was visible` — interface metadata propagated to a constructed implementor
- `__Canon` — shared generic code for reference type arguments

## Building and Running

```cmd
Run.cmd <TestName>
```

Or manually:

```bash
PATH="/c/Program Files (x86)/Microsoft Visual Studio/Installer:$PATH" dotnet publish -c Release -p:TestName=<TestName>
bin\Release\net10.0\win-x64\publish\NativeAotQuirks.exe
```

Run without arguments to list available tests:

```cmd
Run.cmd
```

Each test is a separate file in `tests/` with a `partial class Tests` containing a single `Run()` method. Only one test is compiled at a time (selected via the `TestName` MSBuild property) to ensure complete isolation.
