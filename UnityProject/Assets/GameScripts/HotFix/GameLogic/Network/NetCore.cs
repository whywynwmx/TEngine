using System;
using System.IO;
using System.Threading;
using System.Net.WebSockets;
using System.Collections.Concurrent;
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

    private static int CONNECT_TIMEOUT = 3000;
    private static CancellationTokenSource cancellationTokenSource;
    private static CancellationTokenSource receiveCancellationTokenSource;

    private static ConcurrentQueue<byte[]> recvQueue = new ConcurrentQueue<byte[]>();

    private static SprotoPack sendPack = new SprotoPack();
    private static SprotoPack recvPack = new SprotoPack();

    private static SprotoStream sendStream = new SprotoStream();
    private static SprotoStream recvStream = new SprotoStream();

    public static ProtocolFunctionDictionary protocol => C2sProtocol.Instance.Protocol;
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

            // 为接收操作创建独立的、不超时的CancellationToken
            receiveCancellationTokenSource = new CancellationTokenSource();

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
            receiveCancellationTokenSource?.Cancel();
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
        if (!connected)
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
            while (connected && !receiveCancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), receiveCancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                    {
                        Debug.Log($"Processing {result.Count} bytes of binary data");
                        ProcessReceivedData(receiveBuffer, result.Count);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.LogWarning($"WebSocket close received. Status: {result.CloseStatus}, Description: {result.CloseStatusDescription}");
                        Disconnect();
                        break;
                    }
                }
                catch (WebSocketException wsEx)
                {
                    Debug.LogWarning($"WebSocket exception: {wsEx.Message}");
                    Debug.LogWarning("WebSocket connection lost, disconnecting...");
                    Disconnect();
                    break;
                }
                catch (OperationCanceledException ex)
                {
                    Debug.LogWarning($"WebSocket receive operation was cancelled: {ex.Message}");
                    // 检查是否是连接超时导致的取消
                    if (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Debug.LogWarning("Cancellation was due to connection timeout, stopping receive loop");
                        break;
                    }
                    // 检查是否是接收取消
                    if (receiveCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Debug.LogWarning("Receive operation was cancelled manually, stopping receive loop");
                        break;
                    }
                    Debug.LogWarning("Unexpected cancellation, continuing...");
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    Debug.LogWarning("WebSocket was disposed, connection closed");
                    Disconnect();
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Receive loop error: {e.Message}");
            Debug.LogWarning($"WebSocket State: {webSocket?.State}");
        }
        finally
        {
            Debug.Log("WebSocket receive loop ended");
        }
    }

    private static void ProcessReceivedData(byte[] buffer, int count)
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
                    recvQueue.Enqueue(data);  // 无锁操作
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

    public static void Dispatch()
    {
        Package pkg = new Package();
        int processedCount = 0;

        while (recvQueue.TryDequeue(out byte[] data))
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

            processedCount++;
        }

        if (processedCount > 20)
        {
            Debug.Log($"Processed {processedCount} messages");
        }
    }

}
