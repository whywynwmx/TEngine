using UnityEngine;
using GameLogic;

public class NetworkExample : MonoBehaviour
{
    private NetModule netModule;

    void Start()
    {
        netModule = NetModule.Instance;

        // 订阅重连事件
        netModule.OnReconnectAttempt += OnReconnectAttempt;
        netModule.OnReconnectSuccess += OnReconnectSuccess;
        netModule.OnReconnectFailed += OnReconnectFailed;
        netModule.OnConnectionLost += OnConnectionLost;

        // 配置重连参数
        netModule.SetReconnectConfig(
            enable: true,
            maxAttempts: 5,
            initialDelay: 2f,
            maxDelay: 60f,
            multiplier: 1.5f
        );

        // 连接服务器
        netModule.Connect(
            "localhost",
            8080,
            "ws",
            OnConnected,
            OnConnectFailed
        );
    }

    void OnDestroy()
    {
        if (netModule != null)
        {
            // 取消订阅事件
            netModule.OnReconnectAttempt -= OnReconnectAttempt;
            netModule.OnReconnectSuccess -= OnReconnectSuccess;
            netModule.OnReconnectFailed -= OnReconnectFailed;
            netModule.OnConnectionLost -= OnConnectionLost;

            netModule.Disconnect();
        }
    }

    private void OnConnected()
    {
        Debug.Log("Connected to server successfully!");
        // 这里可以发送登录请求等
    }

    private void OnConnectFailed()
    {
        Debug.LogWarning("Failed to connect to server");
    }

    private void OnReconnectAttempt(int attempt)
    {
        Debug.Log($"Reconnection attempt {attempt}");
        // 更新UI显示重连状态
    }

    private void OnReconnectSuccess()
    {
        Debug.Log("Reconnected successfully!");
        // 可能需要重新发送登录请求
    }

    private void OnReconnectFailed()
    {
        Debug.LogError("All reconnection attempts failed");
        // 显示连接失败UI
    }

    private void OnConnectionLost()
    {
        Debug.LogWarning("Connection lost, attempting to reconnect...");
        // 显示连接中断UI
    }

    void Update()
    {
        // 模拟断线测试
        if (Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log("Simulating disconnection...");
            NetCore.Disconnect();
        }

        // 显示连接状态
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.Log($"Connection Status: Connected={netModule.IsConnected}, Reconnecting={netModule.IsReconnecting}, Attempts={netModule.ReconnectAttempts}");
        }
    }
}