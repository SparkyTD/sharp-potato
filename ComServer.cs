using System.Net;
using System.Net.Sockets;

namespace sharp_potato;

public class ComServer
{
    private readonly TcpListener listener;
    private TcpClient currentClient;
    private readonly byte[] readBuffer = new byte[4096];

    public ComServer(IPEndPoint endPoint)
    {
        listener = new TcpListener(endPoint);
        listener.Server.ReceiveTimeout = 1000;
    }

    public void Start()
    {
        listener.Start();
    }

    public byte[] Read()
    {
        currentClient ??= listener.AcceptTcpClient();

        try
        {
            int read = currentClient.GetStream().Read(readBuffer, 0, readBuffer.Length);
            return readBuffer[..read];
        }
        catch
        {
            return null;
        }
    }

    public void Write(byte[] data) => currentClient.GetStream().Write(data);

    public bool CheckForNewConnections()
    {
        Thread.Sleep(10);
        bool newConnection = listener.Pending();
        if (newConnection)
        {
            currentClient.Close();
            currentClient = listener.AcceptTcpClient();
        }

        return newConnection;
    }

    public void Stop()
    {
        currentClient?.Close();
        listener.Stop();
    }
}