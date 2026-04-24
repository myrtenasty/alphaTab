using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace AlphaTab.Test;

public class TestClassAttribute : Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute
{
    public string DisplayName { get; }

    public TestClassAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}


public class TestMethodAttribute : Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute
{
    public new string DisplayName { get; }

    public TestMethodAttribute(string displayName,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0) : base(displayName)
    {
        DisplayName = displayName;
    }
}

public sealed class TestMethodInfoAccessor
{
    public MethodInfo MethodInfo { get; }

    public TestMethodInfoAccessor(MethodInfo methodInfo)
    {
        MethodInfo = methodInfo;
    }
}

public static class TestMethodAccessor
{
    public static TestMethodInfoAccessor? CurrentTest
    {
        get
        {
            var trace = new StackTrace();
            foreach (var frame in trace.GetFrames() ?? System.Array.Empty<StackFrame>())
            {
                var method = frame.GetMethod() as MethodInfo;
                if (method == null)
                {
                    continue;
                }

                var resolvedMethod = ResolveTestMethod(method);
                if (resolvedMethod != null)
                {
                    return new TestMethodInfoAccessor(resolvedMethod);
                }
            }

            return null;
        }
    }

    private static MethodInfo? ResolveTestMethod(MethodInfo method)
    {
        if (IsTestMethod(method))
        {
            return method;
        }

        if (method.Name != nameof(IAsyncStateMachine.MoveNext) || method.DeclaringType?.DeclaringType == null)
        {
            return null;
        }

        return method.DeclaringType.DeclaringType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
            {
                var stateMachine = candidate.GetCustomAttribute<AsyncStateMachineAttribute>();
                return stateMachine?.StateMachineType == method.DeclaringType && IsTestMethod(candidate);
            });
    }

    private static bool IsTestMethod(MethodInfo method)
    {
        return method.GetCustomAttribute<TestMethodAttribute>() != null ||
               method.GetCustomAttribute<Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute>() != null;
    }
}
