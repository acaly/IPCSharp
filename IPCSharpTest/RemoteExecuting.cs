using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPCSharpTest
{
    public static class RemoteExecuting
    {
        public static async Task<int> Execute(Action action)
        {
            if (action.Method.DeclaringType.Assembly != typeof(RemoteExecuting).Assembly)
            {
                throw new ArgumentException();
            }
            var className = action.Method.DeclaringType.FullName;
            var methodName = action.Method.Name;
            var exePath = typeof(IPCSharpTestHelper.Program).Assembly.Location;
            var dllPath = typeof(RemoteExecuting).Assembly.Location;
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = $"\"{exePath}\" \"{dllPath}\" {className} {methodName}",
                UseShellExecute = true,
            };
            var process = Process.Start(startInfo);
            await Task.Run((Action)process.WaitForExit);
            return process.ExitCode;
        }
    }
}
