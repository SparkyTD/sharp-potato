using System.Net;
using System.Net.Sockets;

namespace sharp_potato;

public class RpcClient
{
    private TcpClient client;
    private readonly IPEndPoint endPoint;
    private readonly byte[] readBuffer = new byte[4096];

    public RpcClient(IPEndPoint endPoint)
    {
        client = new TcpClient();
        this.endPoint = endPoint;
    }

    public void Connect() => client.Connect(endPoint);

    public byte[] Read()
    {
        int read = client.GetStream().Read(readBuffer, 0, readBuffer.Length);
        return readBuffer[..read];
    }

    public void Write(byte[] data) => client.GetStream().Write(data);

    public void ReconnectIfNeeded(ref bool newConnection)
    {
        if (!newConnection)
            return;

        client = new TcpClient();
        client.Connect(endPoint);

        newConnection = false;
    }
}