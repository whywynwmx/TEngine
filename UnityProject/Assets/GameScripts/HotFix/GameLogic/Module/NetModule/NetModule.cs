namespace GameLogic {
    public class NetModule : Singleton<NetModule>, IUpdate
    {
        protected override void OnInit()
        {

        }

        protected override void OnRelease()
        {
            NetCore.Disconnect();
        }

        public void Connect(string host, int port, string protocol = "ws", SocketConnected socketConnected = null, SocketConnectFailed socketConnectFailed = null)
        {
            NetCore.Connect(host, port, protocol, socketConnected, socketConnectFailed);
        }

        public void OnUpdate()
        {
            NetCore.Dispatch();
        }
    }
}