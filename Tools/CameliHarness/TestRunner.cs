using System;
using System.Linq;
using System.Reflection;

// Minimal stand-in for the Unity Test Runner: invokes every [Test] method in this assembly.
static class TestRunner
{
    static int Main()
    {
        int passed = 0, failed = 0;
        foreach (Type type in typeof(TestRunner).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "Cameli.Tests"))
        foreach (MethodInfo method in type.GetMethods().Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute")))
        {
            try
            {
                method.Invoke(Activator.CreateInstance(type), null);
                passed++;
            }
            catch (TargetInvocationException e)
            {
                failed++;
                Console.WriteLine($"FAIL {type.Name}.{method.Name}: {e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
            }
        }
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
