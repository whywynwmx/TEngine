using System;
using System.Collections;
using System.Net.Sockets;
using UnityEngine;

namespace GameLogic {
    public class NetModule : Singleton<NetModule>, IUpdate
    {
        private string currentHost;
        private int currentPort;
        private string currentProtocol;
        private SocketConnected onConnectedCallback;
        private SocketConnectFailed onConnectFailedCallback;

        // 重连配置
        [SerializeField] private bool enableAutoReconnect = true;
        [SerializeField] private int maxReconnectAttempts = 5;
        [SerializeField] private float initialReconnectDelay = 1f;
        [SerializeField] private float maxReconnectDelay = 30f;
        [SerializeField] private float reconnectDelayMultiplier = 2f;
        [SerializeField] private float heartbeatInterval = 5f;
        [SerializeField] private float connectionTimeout = 10f;

        // 重连状态
        private int currentReconnectAttempts = 0;
        private float currentReconnectDelay = 0f;
        private float lastHeartbeatTime = 0f;
        private float lastConnectionCheckTime = 0f;
        private bool isReconnecting = false;
        // 协程状态管理
        private float reconnectTimer = 0f;
        private float heartbeatTimer = 0f;
        private bool isReconnectCoroutineRunning = false;
        private bool isHeartbeatCoroutineRunning = false;

        // 事件
        public event Action<int> OnReconnectAttempt;
        public event Action OnReconnectSuccess;
        public event Action OnReconnectFailed;
        public event Action OnConnectionLost;

        protected override void OnInit()
        {
            currentReconnectDelay = initialReconnectDelay;
        }

        protected override void OnRelease()
        {
            StopAllTimers();
            NetCore.Disconnect();
        }

        public void Connect(string host, int port, string protocol = "ws",
            SocketConnected socketConnected = null, SocketConnectFailed socketConnectFailed = null)
        {
            currentHost = host;
            currentPort = port;
            currentProtocol = protocol;
            onConnectedCallback = socketConnected;
            onConnectFailedCallback = socketConnectFailed;

            // 重置重连状态
            currentReconnectAttempts = 0;
            currentReconnectDelay = initialReconnectDelay;
            isReconnecting = false;

            ConnectInternal();
        }

        private void ConnectInternal()
        {
            StopAllTimers();

            NetCore.Connect(currentHost, currentPort, currentProtocol,
                OnSocketConnected, OnSocketConnectFailed);
        }

        private void OnSocketConnected()
        {
            currentReconnectAttempts = 0;
            currentReconnectDelay = initialReconnectDelay;
            isReconnecting = false;

            // 启动心跳检测
            isHeartbeatCoroutineRunning = true;
            heartbeatTimer = 0f;

            onConnectedCallback?.Invoke();

            if (isReconnecting)
            {
                isReconnecting = false;
                OnReconnectSuccess?.Invoke();
            }
        }

        private void OnSocketConnectFailed()
        {
            onConnectFailedCallback?.Invoke();

            // 主动连接的时候失败不自动重连
            // if (enableAutoReconnect && !isReconnecting)
            // {
            //     StartReconnect();
            // }
        }

        private void StartReconnect()
        {
            if (currentReconnectAttempts >= maxReconnectAttempts)
            {
                Debug.LogWarning($"Max reconnect attempts ({maxReconnectAttempts}) reached");
                OnReconnectFailed?.Invoke();
                return;
            }

            isReconnecting = true;
            currentReconnectAttempts++;
            isReconnectCoroutineRunning = true;
            reconnectTimer = 0f;

            Debug.Log($"Reconnection attempt {currentReconnectAttempts}/{maxReconnectAttempts} in {currentReconnectDelay:F1}s");
            OnReconnectAttempt?.Invoke(currentReconnectAttempts);
        }

        private void UpdateReconnectCoroutine()
        {
            if (!isReconnectCoroutineRunning) return;

            reconnectTimer += Time.deltaTime;

            if (reconnectTimer >= currentReconnectDelay)
            {
                // 执行重连
                NetCore.Disconnect();
                ConnectInternal();

                // 指数退避
                currentReconnectDelay = Mathf.Min(
                    currentReconnectDelay * reconnectDelayMultiplier,
                    maxReconnectDelay
                );

                // 停止重连协程
                isReconnectCoroutineRunning = false;
                reconnectTimer = 0f;
            }
        }

        private void UpdateHeartbeatCoroutine()
        {
            if (!isHeartbeatCoroutineRunning) return;

            heartbeatTimer += Time.deltaTime;

            if (heartbeatTimer >= heartbeatInterval)
            {
                if (NetCore.connected)
                {
                    lastHeartbeatTime = Time.time;
                    SendHeartbeat();
                    heartbeatTimer = 0f;
                }
                else
                {
                    // 连接丢失
                    isHeartbeatCoroutineRunning = false;
                    heartbeatTimer = 0f;
                    OnConnectionLost?.Invoke();
                    if (enableAutoReconnect && !isReconnecting)
                    {
                        StartReconnect();
                    }
                }
            }
        }

        private void SendHeartbeat()
        {
            // 实现心跳包发送逻辑
            Debug.Log("Sending heartbeat...");

            // 这里可以发送实际的心跳包
            // 例如: NetCore.Send<HeartbeatRequest>();
            C2sSprotoType.cs_send_heart_beat request = new C2sSprotoType.cs_send_heart_beat();
            NetSender.Send<C2sSprotoType.cs_send_heart_beat>(request);
        }

        public void OnUpdate()
        {
            NetCore.Dispatch();

            // 更新重连协程
            UpdateReconnectCoroutine();

            // 更新心跳协程
            UpdateHeartbeatCoroutine();

            // 定期检查连接状态
            if (Time.time - lastConnectionCheckTime > 1f)
            {
                lastConnectionCheckTime = Time.time;
                CheckConnectionStatus();
            }
        }

        private void CheckConnectionStatus()
        {
            if (NetCore.connected && !isHeartbeatCoroutineRunning)
            {
                // 如果连接正常但没有心跳，启动心跳
                isHeartbeatCoroutineRunning = true;
                heartbeatTimer = 0f;
            }
            else if (!NetCore.connected && isHeartbeatCoroutineRunning)
            {
                // 如果连接断开但有心跳，停止心跳
                isHeartbeatCoroutineRunning = false;
                heartbeatTimer = 0f;

                OnConnectionLost?.Invoke();
                if (enableAutoReconnect && !isReconnecting)
                {
                    StartReconnect();
                }
            }
        }

        private void StopAllTimers()
        {
            isReconnectCoroutineRunning = false;
            isHeartbeatCoroutineRunning = false;
            reconnectTimer = 0f;
            heartbeatTimer = 0f;
        }

        // 公共方法
        public void Disconnect()
        {
            enableAutoReconnect = false;
            StopAllTimers();
            NetCore.Disconnect();
        }

        public void SetReconnectConfig(bool enable, int maxAttempts = 5,
            float initialDelay = 1f, float maxDelay = 30f, float multiplier = 2f)
        {
            enableAutoReconnect = enable;
            maxReconnectAttempts = maxAttempts;
            initialReconnectDelay = initialDelay;
            maxReconnectDelay = maxDelay;
            reconnectDelayMultiplier = multiplier;
            currentReconnectDelay = initialReconnectDelay;
        }

        public bool IsReconnecting => isReconnecting;
        public int ReconnectAttempts => currentReconnectAttempts;
        public bool IsConnected => NetCore.connected;
    }
}