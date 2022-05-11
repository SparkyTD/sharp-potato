using System.Runtime.InteropServices;

namespace sharp_potato.Secur32;

public static class Native
{
    internal const uint SECPKG_CRED_INBOUND = 0x01;
    internal const uint SECPKG_CRED_OUTBOUND = 0x02;
    internal const uint SECPKG_CRED_BOTH = 0x03;

    internal const uint SEC_E_OK = 0;

    internal const uint ASC_REQ_REPLAY_DETECT = 0x00000004;
    internal const uint ASC_REQ_CONFIDENTIALITY = 0x00000010;
    internal const uint ASC_REQ_USE_SESSION_KEY = 0x00000020;
    internal const uint ASC_REQ_INTEGRITY = 0x00020000;
    internal const uint ASC_REQ_CONNECTION = 0x00000800;
    internal const uint ASC_REQ_ALLOCATE_MEMORY = 0x00000100;

    internal const uint SECURITY_NETWORK_DREP = 0x00;
    internal const uint SECURITY_NATIVE_DREP = 0x10;

    internal const int MAX_TOKEN_SIZE = 12000;

    [DllImport("secur32.dll", SetLastError = true)]
    internal static extern uint AcquireCredentialsHandle(
        string pszPrincipal,
        string pszPackage,
        uint fCredentialUse,
        IntPtr pvLogonID,
        IntPtr pAuthData,
        IntPtr pGetKeyFn,
        IntPtr pvGetKeyArgument,
        out SecHandle phCredential,
        out SECURITY_INTEGER ptsExpiry);

    [DllImport("secur32.dll", SetLastError = true)]
    internal static extern uint AcceptSecurityContext(
        ref SecHandle phCredential,
        IntPtr phContext,
        ref SecBufferDesc pInput,
        uint fContextReq,
        uint TargetDataRep,
        ref Vanara.PInvoke.Secur32.CtxtHandle phNewContext,
        ref SecBufferDesc pOutput,
        out uint pfContextAttr,
        out SECURITY_INTEGER ptsTimeStamp);

    [DllImport("secur32.dll", SetLastError = true)]
    internal static extern uint AcceptSecurityContext(
        ref Vanara.PInvoke.Secur32.CtxtHandle phCredential,
        ref Vanara.PInvoke.Secur32.CtxtHandle phContext,
        ref SecBufferDesc pInput,
        uint fContextReq,
        uint TargetDataRep,
        ref Vanara.PInvoke.Secur32.CtxtHandle phNewContext,
        ref SecBufferDesc pOutput,
        out uint pfContextAttr,
        out SECURITY_INTEGER ptsTimeStamp);

    [StructLayout(LayoutKind.Sequential)]
    public struct SecHandle
    {
        public IntPtr dwLower;
        public IntPtr dwUpper;
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_INTEGER
    {
        public uint LowPart;
        public int HighPart;
    };
}