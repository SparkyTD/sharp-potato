using System.Diagnostics;
using sharp_potato;

var process = new SystemProcess();
process.StartInfo.FileName = "cmd.exe";
process.StartInfo.Arguments = "/K whoami";

process.Start();
process.WaitForExit();

Console.Out.WriteLine("Exited");