using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace WebSocketServer
{
    public class SocketConnection
    {
        public event LogEventHandler Log;
        private string name;
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        private Boolean isDataMasked;
        public Boolean IsDataMasked
        {
            get { return isDataMasked; }
            set { isDataMasked = value; }
        }

        public TCPServer ConnectionSocket;
        public uint clientindex;
       // public Socket ConnectionSocket;

        private int MaxBufferSize;
        private string Handshake;
        private string New_Handshake;

       // public byte[] receivedDataBuffer;
        private byte[] FirstByte;
        private byte[] LastByte;
        private byte[] ServerKey1;
        private byte[] ServerKey2;


        public event NewConnectionEventHandler NewConnection;
        public event DataReceivedEventHandler DataReceived;
        public event DisconnectedEventHandler Disconnected;

        public SocketConnection()
        {
            MaxBufferSize = 1024 * 100;
           // receivedDataBuffer = new byte[MaxBufferSize];
            FirstByte = new byte[MaxBufferSize];
            LastByte = new byte[MaxBufferSize];
            FirstByte[0] = 0x00;
            LastByte[0] = 0xFF;

            Handshake = "HTTP/1.1 101 Web Socket Protocol Handshake" + CrestronEnvironment.NewLine;// Environment.NewLine;
            Handshake += "Upgrade: WebSocket" + CrestronEnvironment.NewLine;
            Handshake += "Connection: Upgrade" + CrestronEnvironment.NewLine;
            Handshake += "Sec-WebSocket-Origin: " + "{0}" + CrestronEnvironment.NewLine;
            Handshake += string.Format("Sec-WebSocket-Location: " + "ws://{0}:7001/chat" + CrestronEnvironment.NewLine, WebSocketServer.getLocalmachineIPAddress());
            Handshake += CrestronEnvironment.NewLine;

            New_Handshake = "HTTP/1.1 101 Switching Protocols" + CrestronEnvironment.NewLine;
            New_Handshake += "Upgrade: WebSocket" + CrestronEnvironment.NewLine;
            New_Handshake += "Connection: Upgrade" + CrestronEnvironment.NewLine;
            New_Handshake += "Sec-WebSocket-Accept: {0}" + CrestronEnvironment.NewLine;
            New_Handshake += CrestronEnvironment.NewLine;
        }
        #region 接收正式数据
        private void Read(TCPServer myTCPServer, uint clientIndex, int numberOfBytesReceived)
        {
            if (!myTCPServer.ClientConnected(clientindex)) return;
            string messageReceived = string.Empty;
            byte[] readBuffer = new byte[numberOfBytesReceived];
            Array.Copy(myTCPServer.GetIncomingDataBufferForSpecificClient(clientindex), readBuffer, numberOfBytesReceived);
            
            DataFrame dr = new DataFrame(readBuffer);
            try
            {
                if (!this.isDataMasked)
                {
                    // Web Socket protocol: messages are sent with 0x00 and 0xFF as padding bytes
                    int startIndex = 0;
                    int endIndex = 0;
                    // Search for the start byte
                    while (readBuffer[startIndex] == FirstByte[0]) startIndex++;
                    // Search for the end byte
                    endIndex = startIndex + 1;
                    while (readBuffer[endIndex] != LastByte[0] && endIndex != MaxBufferSize - 1) endIndex++;
                    if (endIndex == MaxBufferSize - 1) endIndex = MaxBufferSize;

                    // Get the message
                    messageReceived =Encoding.GetEncoding(28591).GetString(myTCPServer.GetIncomingDataBufferForSpecificClient(clientindex), startIndex, endIndex - startIndex);
                }
                else
                {
                    messageReceived = dr.Text;
                }

                //if ((messageReceived.Length == MaxBufferSize && messageReceived[0] == Convert.ToChar(65533)) ||
                //    messageReceived.Length == 0)
                //{
                //    if (Disconnected != null)
                //        Disconnected(this, EventArgs.Empty);
                //}
                //else
                {
                    if (DataReceived != null)
                    {
                        DataReceived(this, messageReceived, EventArgs.Empty);
                    }
                    Array.Clear(readBuffer, 0, readBuffer.Length);
                    myTCPServer.ReceiveDataAsync(clientindex, this.Read, 0);
                } 
            }
            catch (Exception ex)
            {
                if (this.Log != null)
                {
                    this.Log("ReadException:" + ex.Message);
                }
                if (Disconnected != null)
                    Disconnected(this, EventArgs.Empty);
            }
        }
        
        #endregion
   
        private void BuildServerPartialKey(int keyNum, string clientKey)
        {
            string partialServerKey = "";
            byte[] currentKey;
            int spacesNum = 0;
            char[] keyChars = clientKey.ToCharArray();
            foreach (char currentChar in keyChars)
            {
                if (char.IsDigit(currentChar)) partialServerKey += currentChar;
                if (char.IsWhiteSpace(currentChar)) spacesNum++;
            }
            try
            {
                currentKey = BitConverter.GetBytes((int)(Int64.Parse(partialServerKey) / spacesNum));
                if (BitConverter.IsLittleEndian) Array.Reverse(currentKey);

                if (keyNum == 1) ServerKey1 = currentKey;
                else ServerKey2 = currentKey;
            }
            catch
            {
                if (ServerKey1 != null) Array.Clear(ServerKey1, 0, ServerKey1.Length);
                if (ServerKey2 != null) Array.Clear(ServerKey2, 0, ServerKey2.Length);
            }
        }

        private byte[] BuildServerFullKey(byte[] last8Bytes)
        {
            byte[] concatenatedKeys = new byte[16];
            Array.Copy(ServerKey1, 0, concatenatedKeys, 0, 4);
            Array.Copy(ServerKey2, 0, concatenatedKeys, 4, 4);
            Array.Copy(last8Bytes, 0, concatenatedKeys, 8, 8);

            // MD5 Hash
            Crestron.SimplSharp.Cryptography.MD5 MD5Service = Crestron.SimplSharp.Cryptography.MD5.Create();
            return MD5Service.ComputeHash(concatenatedKeys);
        }

        public static String ComputeWebSocketHandshakeSecurityHash09(String secWebSocketKey)
        {
            const String MagicKEY = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            String secWebSocketAccept = String.Empty;
            // 1. Combine the request Sec-WebSocket-Key with magic key.
            String ret = secWebSocketKey + MagicKEY;
            // 2. Compute the SHA1 hash
            Crestron.SimplSharp.Cryptography.SHA1 sha = new Crestron.SimplSharp.Cryptography.SHA1CryptoServiceProvider();
            byte[] sha1Hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ret));
            // 3. Base64 encode the hash
            secWebSocketAccept = Convert.ToBase64String(sha1Hash);
            return secWebSocketAccept;
        }



        //void ReceivedCallBack(TCPServer myTCPServer, uint clientIndex, int numberOfBytesReceived)

        public void ManageHandshake(TCPServer myTCPServer, uint clientIndex, int numberOfBytesReceived)
        {
   
            if (myTCPServer.ClientConnected(clientindex))
            {
                string header = "Sec-WebSocket-Version:";
                int HandshakeLength = numberOfBytesReceived;// myTCPServer.IncomingDataBuffer.Length;// (int)status.AsyncState;
                byte[] last8Bytes = new byte[8];
                //Encoding.UTF8 decoder = new System.Text.UTF8Encoding();
                byte[] receivedbytes=myTCPServer.GetIncomingDataBufferForSpecificClient(clientindex);
                String rawClientHandshake = Encoding.GetEncoding(28591).GetString(receivedbytes, 0, HandshakeLength);
                Array.Copy(receivedbytes, HandshakeLength - 8, last8Bytes, 0, 8);
                //现在使用的是比较新的Websocket协议
                if (rawClientHandshake.IndexOf(header) != -1)
                {
     
                    this.isDataMasked = true;
                    // string[] rawClientHandshakeLines = rawClientHandshake.Split(new string[] { Environment.NewLine }, System.StringSplitOptions.RemoveEmptyEntries);
                    string acceptKey = "";

                    //  string key = string.Empty;

                    System.Text.RegularExpressions.Regex r = new System.Text.RegularExpressions.Regex(@"Sec\-WebSocket\-Key:(.*?)\r\n"); //查找"Abc"
                    System.Text.RegularExpressions.Match m = r.Match(rawClientHandshake); //设定要查找的字符串
                    if (m.Groups.Count != 0)
                    {
                        acceptKey = System.Text.RegularExpressions.Regex.Replace(m.Value, @"Sec\-WebSocket\-Key:(.*?)\r\n", "$1").Trim();
                    }
                    acceptKey = ComputeWebSocketHandshakeSecurityHash09(acceptKey);
                    New_Handshake = string.Format(New_Handshake, acceptKey);
                    byte[] newHandshakeText = Encoding.GetEncoding(28591).GetBytes(New_Handshake);

               
                    myTCPServer.SendDataAsync(clientindex, newHandshakeText, 0, newHandshakeText.Length, HandshakeFinished, null);
                    return;
                }

                string ClientHandshake = Encoding.GetEncoding(28591).GetString(myTCPServer.GetIncomingDataBufferForSpecificClient(clientindex), 0, HandshakeLength - 8);
                System.Text.RegularExpressions.Regex r1 = new System.Text.RegularExpressions.Regex(@"Sec\-WebSocket\-Key1:(.*?)\r\n"); //查找"Abc"
                System.Text.RegularExpressions.Match m1 = r1.Match(rawClientHandshake); //设定要查找的字符串
                if (m1.Groups.Count != 0)
                {
                    BuildServerPartialKey(1, System.Text.RegularExpressions.Regex.Replace(m1.Value, @"Sec\-WebSocket\-Key1:(.*?)\r\n", "$1").Trim());

                }
           
                System.Text.RegularExpressions.Regex r2 = new System.Text.RegularExpressions.Regex(@"Sec\-WebSocket\-Key1:(.*?)\r\n"); //查找"Abc"
                System.Text.RegularExpressions.Match m2 = r2.Match(rawClientHandshake); //设定要查找的字符串
                if (m2.Groups.Count != 0)
                {
                    BuildServerPartialKey(2, System.Text.RegularExpressions.Regex.Replace(m2.Value, @"Sec\-WebSocket\-Key1:(.*?)\r\n", "$1").Trim());

                }
                System.Text.RegularExpressions.Regex r3 = new System.Text.RegularExpressions.Regex(@"Origin:(.*?)\r\n"); //查找"Abc"
                System.Text.RegularExpressions.Match m3 = r3.Match(rawClientHandshake); //设定要查找的字符串
                if (m3.Groups.Count != 0)
                {
                    Handshake = string.Format(Handshake, System.Text.RegularExpressions.Regex.Replace(m3.Value, @"Origin:(.*?)\r\n", "$1").Trim());

                }
                else
                {
                    Handshake = string.Format(Handshake, "null");
                }
                // string[] ClientHandshakeLines = ClientHandshake.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                //   logger.Log("新的连接请求来自" + ConnectionSocket.LocalEndPoint + "。正在准备连接 ...");

                // Welcome the new client
                //foreach (string Line in ClientHandshakeLines)
                //{
                //    logger.Log(Line);
                //    if (Line.Contains("Sec-WebSocket-Key1:"))
                //        BuildServerPartialKey(1, Line.Substring(Line.IndexOf(":") + 2));
                //    if (Line.Contains("Sec-WebSocket-Key2:"))
                //        BuildServerPartialKey(2, Line.Substring(Line.IndexOf(":") + 2));
                //    if (Line.Contains("Origin:"))
                //        try
                //        {
                //             Handshake = string.Format(Handshake, Line.Substring(Line.IndexOf(":") + 2));
                //        }
                //        catch
                //        {
                //            Handshake = string.Format(Handshake, "null");
                //        }
                //}
                //// Build the response for the client
                byte[] HandshakeText = Encoding.UTF8.GetBytes(Handshake);
                byte[] serverHandshakeResponse = new byte[HandshakeText.Length + 16];
                byte[] serverKey = BuildServerFullKey(last8Bytes);
                Array.Copy(HandshakeText, serverHandshakeResponse, HandshakeText.Length);
                Array.Copy(serverKey, 0, serverHandshakeResponse, HandshakeText.Length, 16);

       
                //logger.Log("发送握手信息 ...");
                myTCPServer.SendDataAsync(clientindex, serverHandshakeResponse, 0, serverHandshakeResponse.Length, HandshakeFinished, null);

                //  ConnectionSocket.BeginSend(serverHandshakeResponse, 0, HandshakeText.Length + 16, 0, HandshakeFinished, null);
                //logger.Log(Handshake);
            }
     
        }
        /// <summary>
        /// 握手协议完成
        /// </summary>
        /// <param name="status"></param>
        private void HandshakeFinished(TCPServer myTCPServer, uint clientIndex, int numberOfBytesSent, object userSpecifiedObject)
        {

            myTCPServer.ReceiveDataAsync(clientindex, this.Read, 0);
          //  ConnectionSocket.EndSend(status);
           // ConnectionSocket.BeginReceive(receivedDataBuffer, 0, receivedDataBuffer.Length, 0, new AsyncCallback(Read), null);
            if (NewConnection != null) NewConnection(EventArgs.Empty);
        }
        #region SocketClose
        public void SocketClose()
        {
            this.ConnectionSocket.Disconnect(clientindex);
        }
        #endregion

    }
}