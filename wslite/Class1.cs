// Decompiled with JetBrains decompiler
// Type: WebsocketServer.WebsocketSrvr
// Assembly: WebsocketServer, Version=1.0.0.27676, Culture=neutral, PublicKeyToken=null
// MVID: 9416C711-8AFD-444F-9A24-232C417E9E26
// Assembly location: C:\Users\artna\Desktop\WebsocketServer.dll

using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace WebsocketServer
{
    public delegate void StringCallback(SimplSharpString msg);

    public interface IListener
    {
        void sendTrace(string msg);

        void sendTrace(byte[] msg);
    }

    public class WebsocketSrvr : IListener
    {
        private bool restartConnection = false;
        private bool isOnline = false;
        private const int SERVER_PORT = 8080;
        private const int BUFFER_SIZE = 1024;
        private const int MAX_CONNECTION = 1;
        private TCPServer server;
        private ByteBuffer myBuffer;

        public StringCallback SendTrace { get; set; }

        public StringCallback RecieveMessage { get; set; }

        public WebsocketSrvr()
        {
            this.server = new TCPServer("0.0.0.0", 8080, 1024);
            this.myBuffer = new ByteBuffer();
        }

        public void Initialize(int port)
        {
            if (port <= 0)
                return;
            //this.server.set_PortNumber(port);
        }

        public void StartServer()
        {
            this.restartConnection = true;
            // ISSUE: method pointer
            this.server.WaitForConnectionAsync(tcpServerClientConnectCallback);
        }

        public void StopServer()
        {
            this.restartConnection = false;
            this.server.DisconnectAll();
            this.myBuffer.Clear();
        }

        public void SetDigitalSignal(ushort signal, ushort state)
        {
            byte[] numArray = state <= (ushort)0 ? WebsocketUtil.EncodeMsg((byte)129, StringUtil.toByteArray("OFF[" + (object)signal + "]")) : WebsocketUtil.EncodeMsg((byte)129, StringUtil.toByteArray("ON[" + (object)signal + "]"));
            if (this.server.ServerSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                return;
            this.server.SendData(numArray, numArray.Length);
        }

        public void SetAnalogSignal(ushort signal, ushort value)
        {
            byte[] numArray = WebsocketUtil.EncodeMsg((byte)129, StringUtil.toByteArray("LEVEL[" + (object)signal + "," + (object)value + "]"));
            if (this.server.ServerSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                return;
            this.server.SendData(numArray, numArray.Length);
        }

        public void SetIndirectTextSignal(ushort signal, string text)
        {
            byte[] numArray = WebsocketUtil.EncodeMsg((byte)129, StringUtil.toByteArray("STRING[" + (object)signal + "," + text + "]"));
            if (this.server.ServerSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                return;
            this.server.SendData(numArray, numArray.Length);
        }

        private void resetConnection(uint clientIndex)
        {
            this.server.Disconnect(clientIndex);
            this.myBuffer.Clear();
            this.isOnline = false;
            if (!this.restartConnection)
                return;
            // ISSUE: method pointer
            this.server.WaitForConnectionAsync(tcpServerClientConnectCallback);
        }

        private void tcpServerClientConnectCallback(TCPServer myTCPServer, uint clientIndex)
        {
            // ISSUE: method pointer
            this.server.ReceiveDataAsync(tcpServerReceiveCallback);
        }

        private void tcpServerReceiveCallback(
          TCPServer myTCPServer,
          uint clientIndex,
          int numberOfBytesReceived)
        {
            if (numberOfBytesReceived == 0)
            {
                this.resetConnection(clientIndex);
            }
            else
            {
                this.myBuffer.Append(myTCPServer.GetIncomingDataBufferForSpecificClient(clientIndex), numberOfBytesReceived);
                if (!this.isOnline)
                {
                    if (this.myBuffer.IndexOf("\r\n\r\n") > 0)
                    {
                        string boundString = StringUtil.getBoundString(this.myBuffer.ToString(), "Sec-WebSocket-Key: ", "\r\n");
                        if (boundString != null && boundString.Length > 0)
                        {
                            byte[] numArray = WebsocketUtil.buildHandshakeMessage(boundString);
                            myTCPServer.SendData(clientIndex, numArray, numArray.Length);
                            this.myBuffer.Clear();
                            this.isOnline = true;
                        }
                        else
                            this.resetConnection(clientIndex);
                    }
                }
                else
                {
                    while (true)
                    {
                        uint num1 = WebsocketUtil.DecodeLength(this.myBuffer.ToArray());
                        if ((long)this.myBuffer.Length() >= (long)num1 && num1 != 0U)
                        {
                            byte[] array = this.myBuffer.ToArray(0, (int)num1);
                            this.myBuffer.Delete(0, (int)num1);
                            byte num2 = array[0];
                            byte[] numArray1 = WebsocketUtil.DecodeMsg(array);
                            switch (num2)
                            {
                                case 129:
                                    string msg = StringUtil.toString(numArray1);
                                    if (this.RecieveMessage != null)
                                        this.RecieveMessage(msg);
                                    break;
                                case 136:
                                    this.resetConnection(clientIndex);
                                    break;
                                case 137:
                                    byte[] numArray2 = WebsocketUtil.EncodeMsg((byte)138, numArray1);
                                    myTCPServer.SendData(clientIndex, numArray2, numArray2.Length);
                                    break;
                            }
                        }
                        else
                            break;
                    }
                }
                // ISSUE: method pointer
                this.server.ReceiveDataAsync(clientIndex, new TCPServerReceiveCallback(tcpServerReceiveCallback));
            }
        }

        private void sendTrace(SimplSharpString msg)
        {
            if (this.SendTrace == null)
                return;
            this.SendTrace(msg);
        }

        void IListener.sendTrace(string msg)
        {
            this.sendTrace(msg);
        }

        void IListener.sendTrace(byte[] msg)
        {
            this.sendTrace(StringUtil.toHexString(msg));
        }
    }
}
