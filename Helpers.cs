namespace NativeAotQuirks;

public static class Helpers
{
    // Fakes mutation - use it to block NativeAot compiler from using compile-time heuristic based on constant
    public static T M<T>(T value) => new[] { value, default! }[CodeKeeper.AlwaysTrue ? 0 : 1];

    public static object ActivateGeneric(Type type, params Type[] argTypes)
    {
        type = M(type);
        argTypes = argTypes.Select(M).ToArray();
        var closed = type.MakeGenericType(argTypes);
        return Activator.CreateInstance(closed)!;
    }

    public static object Activate(Type type, params Type[] argTypes)
    {
        type = M(type);
        argTypes = argTypes.Select(M).ToArray();
        var flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;
        var ctor = type.GetConstructor(flags, null, argTypes, null)
            ?? throw new MissingMethodException(
                $"No constructor({string.Join(", ", argTypes.Select(t => t.Name))}) on {type.Name}");
        var args = argTypes.Select(t => t.IsValueType ? Activator.CreateInstance(t) : null).ToArray();
        return ctor.Invoke(args)!;
    }

    public static void TestMembers(object instanceOrType, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
            TestMember(instanceOrType, memberName);
    }

    public static void TestMember(object instanceOrType, string memberName)
    {
        var isStatic = instanceOrType is Type;
        var type = isStatic ? (Type)instanceOrType : instanceOrType.GetType();
        var instance = isStatic ? null : instanceOrType;
        var flags = (isStatic ? System.Reflection.BindingFlags.Static : System.Reflection.BindingFlags.Instance)
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        var field = type.GetField(memberName, flags);
        if (field != null)
        {
            var val = field.GetValue(instance);
            Console.WriteLine($"    Field {memberName}: read OK ({val})");
            if (!field.IsInitOnly)
            {
                var defaultVal = field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null;
                field.SetValue(instance, defaultVal);
                Console.WriteLine($"    Field {memberName}: write OK");
            }
            return;
        }

        var prop = type.GetProperty(memberName, flags);
        if (prop != null)
        {
            if (prop.GetMethod != null)
            {
                var val = prop.GetValue(instance);
                Console.WriteLine($"    Property {memberName}: get OK ({val})");
            }
            if (prop.SetMethod != null)
            {
                var defaultVal = prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null;
                prop.SetValue(instance, defaultVal);
                Console.WriteLine($"    Property {memberName}: set OK");
            }
            return;
        }

        var method = type.GetMethod(memberName, flags);
        if (method != null)
        {
            var parameters = method.GetParameters();
            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var pt = parameters[i].ParameterType;
                args[i] = parameters[i].HasDefaultValue
                    ? parameters[i].DefaultValue
                    : pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            var result = method.Invoke(instance, args);
            Console.WriteLine($"    Method {memberName}: call OK ({result})");
            return;
        }

        Console.WriteLine($"    {memberName}: NOT FOUND");
    }

    public static void TestInterfaceMembers(object instance, Type interfaceType, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
            TestInterfaceMember(instance, interfaceType, memberName);
    }

    public static void TestInterfaceMember(object instance, Type interfaceType, string memberName)
    {
        interfaceType = M(interfaceType);
        var flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;

        var prop = interfaceType.GetProperty(memberName, flags);
        if (prop != null)
        {
            if (prop.GetMethod != null)
            {
                var val = prop.GetMethod.Invoke(instance, null);
                Console.WriteLine($"    Property {memberName}: get OK ({val})");
            }
            if (prop.SetMethod != null)
            {
                var defaultVal = prop.PropertyType.IsValueType ? Activator.CreateInstance(prop.PropertyType) : null;
                prop.SetMethod.Invoke(instance, [defaultVal]);
                Console.WriteLine($"    Property {memberName}: set OK");
            }
            return;
        }

        var method = interfaceType.GetMethod(memberName, flags);
        if (method != null)
        {
            var parameters = method.GetParameters();
            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var pt = parameters[i].ParameterType;
                args[i] = parameters[i].HasDefaultValue
                    ? parameters[i].DefaultValue
                    : pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            var result = method.Invoke(instance, args);
            Console.WriteLine($"    Method {memberName}: call OK ({result})");
            return;
        }

        Console.WriteLine($"    {memberName}: NOT FOUND");
    }

    public static void Test(string title, Func<object> action)
    {
        try
        {
            var inst = action();
            Console.WriteLine($"  {title}: OK ({inst})");
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            var msg = inner.Message.Length > 60 ? inner.Message[..60] + "..." : inner.Message;
            Console.WriteLine($"  {title}: FAIL ({inner.GetType().Name}: {msg})");
        }
    }
}
