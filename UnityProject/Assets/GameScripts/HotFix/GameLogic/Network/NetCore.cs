using System;
using System.IO;
using System.Threading;
using System.Net.WebSockets;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sproto;
using SprotoType;
using UnityEngine;

public delegate void SocketConnected();
public delegate void SocketConnectFailed();

public class NetCore
{
    private static ClientWebSocket webSocket;

    public static bool logined;
    public static bool enabled;

    private static int CONNECT_TIMEOUT = 3000;
    private static CancellationTokenSource cancellationTokenSource;

    private static Queue<byte[]> recvQueue = new Queue<byte[]>();

    private static SprotoPack sendPack = new SprotoPack();
    private static SprotoPack recvPack = new SprotoPack();

    private static SprotoStream sendStream = new SprotoStream();
    private static SprotoStream recvStream = new SprotoStream();

    //private static ProtocolFunctionDictionary protocol = Protocol.Instance.Protocol;
    public static ProtocolFunctionDictionary protocol = new ProtocolFunctionDictionary();
    private static Dictionary<long, ProtocolFunctionDictionary.typeFunc> sessionDict;

    private static byte[] receiveBuffer = new byte[1 << 16];

    public static void Init()
    {
        recvStream.Write(receiveBuffer, 0, receiveBuffer.Length);
        recvStream.Seek(0, SeekOrigin.Begin);

        sessionDict = new Dictionary<long, ProtocolFunctionDictionary.typeFunc>();
    }

    public static async void Connect(string host, int port, string protocol = "ws", SocketConnected socketConnected = null, SocketConnectFailed socketConnectFailed = null)
    {
        Disconnect();

        try
        {
            webSocket = new ClientWebSocket();
            cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(CONNECT_TIMEOUT);

            string uri = $"{protocol}://{host}:{port}";
            await webSocket.ConnectAsync(new Uri(uri), cancellationTokenSource.Token);

            if (webSocket.State == WebSocketState.Open)
            {
                Receive();
                socketConnected();
            }
            else
            {
                Debug.Log("WebSocket connection failed");
                socketConnectFailed();
            }
        }
        catch (Exception e)
        {
            Debug.Log($"Connect Timeout or Error: {e.Message}");
            socketConnectFailed();
        }
    }

    public static void Disconnect()
    {
        if (connected)
        {
            cancellationTokenSource?.Cancel();
            webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            webSocket?.Dispose();
            webSocket = null;
        }
    }

    public static bool connected
    {
        get
        {
            return webSocket != null && webSocket.State == WebSocketState.Open;
        }
    }

    public static void Send<T>(SprotoTypeBase rpc = null, long? session = null)
    {
        Send(rpc, session, protocol[typeof(T)]);
    }

    private static int MAX_PACK_LEN = (1 << 16) - 1;
    private static void Send(SprotoTypeBase rpc, long? session, int tag)
    {
        if (!connected || !enabled)
        {
            return;
        }

        Package pkg = new Package();
        pkg.type = tag;

        if (session != null)
        {
            pkg.session = (long)session;
            sessionDict.Add((long)session, protocol[tag].Response.Value);
        }

        sendStream.Seek(0, SeekOrigin.Begin);
        int len = pkg.encode(sendStream);
        if (rpc != null)
        {
            len += rpc.encode(sendStream);
        }

        byte[] data = sendPack.pack(sendStream.Buffer, len);
        if (data.Length > MAX_PACK_LEN)
        {
            Debug.Log("data.Length > " + MAX_PACK_LEN + " => " + data.Length);
            return;
        }

        sendStream.Seek(0, SeekOrigin.Begin);
        sendStream.WriteByte((byte)(data.Length >> 8));
        sendStream.WriteByte((byte)data.Length);
        sendStream.Write(data, 0, data.Length);

        try {
            var dataToSend = new byte[sendStream.Position];
            Array.Copy(sendStream.Buffer, dataToSend, sendStream.Position);
            webSocket.SendAsync(new ArraySegment<byte>(dataToSend), WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
        }
        catch (Exception e) {
            Debug.LogWarning(e.ToString());
        }
    }

    private static int receivePosition;
    public static async void Receive()
    {
        if (!connected)
        {
            return;
        }

        try
        {
            while (connected && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                {
                    ProcessReceivedData(receiveBuffer, result.Count);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Disconnect();
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Receive error: {e.ToString()}");
        }
    }

    private static void ProcessReceivedData(byte[] buffer, int count)
    {
        lock (recvQueue)
        {
            int bufferPos = 0;

            while (bufferPos < count)
            {
                int copyLength = Math.Min(count - bufferPos, recvStream.Buffer.Length - receivePosition);
                if (copyLength > 0)
                {
                    recvStream.Seek(receivePosition, SeekOrigin.Begin);
                    recvStream.Write(buffer, bufferPos, copyLength);
                    receivePosition += copyLength;
                    bufferPos += copyLength;
                }

                int i = 0;
                while (receivePosition >= i + 2)
                {
                    int length = (recvStream[i] << 8) | recvStream[i+1];

                    int sz = length + 2;
                    if (receivePosition < i + sz)
                    {
                        break;
                    }

                    recvStream.Seek(i + 2, SeekOrigin.Begin);

                    if (length > 0)
                    {
                        byte[] data = new byte[length];
                        recvStream.Read(data, 0, length);
                        recvQueue.Enqueue(data);
                    }

                    i += sz;
                }

                if (i > 0)
                {
                    recvStream.Seek(0, SeekOrigin.Begin);
                    recvStream.MoveUp(i, receivePosition - i);
                    receivePosition -= i;
                }
            }
        }
    }

    public static void Dispatch()
    {
        Package pkg = new Package();
        List<byte[]> messagesToProcess = new List<byte[]>();

        lock (recvQueue)
        {
            if (recvQueue.Count > 20)
            {
                Debug.Log("recvQueue.Count: " + recvQueue.Count);
            }

            while (recvQueue.Count > 0)
            {
                messagesToProcess.Add(recvQueue.Dequeue());
            }
        }

        foreach (byte[] data in messagesToProcess)
        {
            byte[] unpackedData = recvPack.unpack(data);
            int offset = pkg.init(unpackedData);

            int tag = (int)pkg.type;
            long session = (long)pkg.session;

            if (pkg.HasType)
            {
                RpcReqHandler rpcReqHandler = NetReceiver.GetHandler(tag);
                if (rpcReqHandler != null)
                {
                    SprotoTypeBase rpcRsp = rpcReqHandler(protocol.GenRequest(tag, unpackedData, offset));
                    if (pkg.HasSession)
                    {
                        Send(rpcRsp, session, tag);
                    }
                }
            }
            else
            {
                RpcRspHandler rpcRspHandler = NetSender.GetHandler(session);
                if (rpcRspHandler != null)
                {
                    ProtocolFunctionDictionary.typeFunc GenResponse;
                    sessionDict.TryGetValue(session, out GenResponse);
                    rpcRspHandler(GenResponse(unpackedData, offset));
                }
            }
        }
    }

}
