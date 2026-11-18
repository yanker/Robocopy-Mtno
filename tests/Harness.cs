using System;
using System.Collections.Generic;
using System.Reflection;

namespace CopiaSegura.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    public sealed class CheckFailedException : Exception
    {
        public CheckFailedException(string message) : base(message) { }
    }

    public static class Check
    {
        public static void Equal(object expected, object actual, string msg = "")
        {
            if (!object.Equals(expected, actual))
                throw new CheckFailedException(msg + " esperado <" + expected + "> pero fue <" + actual + ">");
        }

        public static void True(bool condition, string msg = "")
        {
            if (!condition) throw new CheckFailedException("condicion falsa. " + msg);
        }

        public static void Throws<T>(Action a) where T : Exception
        {
            try { a(); }
            catch (T) { return; }
            catch (Exception e)
            {
                throw new CheckFailedException("se esperaba " + typeof(T).Name + " pero se lanzo " + e.GetType().Name);
            }
            throw new CheckFailedException("se esperaba " + typeof(T).Name + " pero no se lanzo nada");
        }
    }

    public static class Harness
    {
        public static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : "";
            int total = 0, passed = 0;
            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.GetCustomAttributes(typeof(TestAttribute), false).Length == 0) continue;
                    string name = t.Name + "." + m.Name;
                    if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    total++;
                    try { m.Invoke(null, null); passed++; }
                    catch (TargetInvocationException e)
                    {
                        Console.WriteLine("FAIL " + name + ": " + e.InnerException.Message);
                    }
                }
            }
            Console.WriteLine("PASSED " + passed + "/" + total);
            return (passed == total && total > 0) ? 0 : 1;
        }
    }
}
