using System;
using System.Reflection;

namespace IPCSharpTestHelper
{
    public static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Environment.Exit(2);
            }
            try
            {
                var assemblyPath = args[1];
                var className = args[2];
                var methodName = args[3];
                var type = Assembly.LoadFile(assemblyPath).GetType(className);
                var method = type.GetMethod(methodName);
                if (method.IsStatic)
                {
                    method.Invoke(null, new object[0]);
                }
                else
                {
                    method.Invoke(Activator.CreateInstance(type), new object[0]);
                }
            }
            catch
            {
                Environment.Exit(1);
            }
        }
    }
}
