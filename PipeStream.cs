using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;
using static Vanara.PInvoke.AdvApi32;
using static Vanara.PInvoke.Kernel32;
using static Vanara.PInvoke.Secur32;

namespace sharp_potato;

public class PipeStream : Stream
{
    private readonly SafeHPIPE pipeHandle;

    public PipeStream(SafeHPIPE pipeHandle, bool isReadStream)
    {
        this.pipeHandle = pipeHandle;

        CanRead = isReadStream;
        CanWrite = !isReadStream;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var tempBuffer = new byte[count];
        ReadFile(pipeHandle, tempBuffer, (uint) count, out var bytesRead);
        Array.Copy(tempBuffer, 0, buffer, offset, count);
        return (int) bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var tempBuffer = new byte[count];
        Array.Copy(buffer, offset, tempBuffer, 0, count);
        WriteFile(pipeHandle, tempBuffer, (uint) count, out var written);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Close()
    {
        CloseHandle(pipeHandle.DangerousGetHandle());
    }

    public override bool CanRead { get; }
    public override bool CanWrite { get; }
    public override bool CanSeek { get; }
    public override long Length { get; }
    public override long Position { get; set; }
}