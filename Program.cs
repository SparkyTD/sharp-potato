using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using sharp_potato;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

var process = new SystemProcess();
process.StartInfo.FileName = "cmd.exe";
process.StartInfo.Arguments = "/K whoami";
process.StartInfo.RedirectStandardOutput = true;
process.StartInfo.RedirectStandardError = true;
process.StartInfo.RedirectStandardInput = true;
process.StartInfo.CreateNoWindow = true;

process.Start();

new Thread(() =>
{
    var buffer = new char[512]; 
    while (true)
    {
        int charsRead = process.StandardOutput.Read(buffer);
        Console.Out.Write(new string(buffer.Take(charsRead).ToArray()));
    }
}).Start();

new Thread(() =>
{
    var buffer = new char[512];
    while (true)
    {
        int charsRead = process.StandardError.Read(buffer);
        Console.Error.Write(new string(buffer.Take(charsRead).ToArray()));
    }
}).Start();

while (true)
{
    var line = Console.ReadLine();
    process.StandardInput.WriteLine(line);
}

Console.Out.WriteLine("Exited");