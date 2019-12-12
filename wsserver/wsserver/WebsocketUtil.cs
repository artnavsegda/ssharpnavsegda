// Decompiled with JetBrains decompiler
// Type: WebsocketServer.WebsocketUtil
// Assembly: WebsocketServer, Version=1.0.0.27676, Culture=neutral, PublicKeyToken=null
// MVID: 9416C711-8AFD-444F-9A24-232C417E9E26
// Assembly location: C:\Users\artna\Desktop\WebsocketServer.dll

using System;

namespace WebsocketServer
{
    public class WebsocketUtil
    {
        private const int MAX_BUFFER = 16384;
        private const string magicKey = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        public const byte MT_MSG = 129;
        public const byte MT_MSG_FRAGMENT = 1;
        public const byte MT_DISCONNECT = 136;
        public const byte MT_PING = 137;
        public const byte MT_PONG = 138;

        public static byte[] buildHandshakeMessage(string seq)
        {
            string base64String = Convert.ToBase64String(new SHA1Util().Hash(StringUtil.toByteArray(seq + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            ByteBuffer byteBuffer = new ByteBuffer();
            byteBuffer.Append(StringUtil.toByteArray("HTTP/1.1 101 Switching Protocols\r\n"));
            byteBuffer.Append(StringUtil.toByteArray("Upgrade: websocket\r\n"));
            byteBuffer.Append(StringUtil.toByteArray("Connection: Upgrade\r\n"));
            byteBuffer.Append(StringUtil.toByteArray("Sec-WebSocket-Accept:" + base64String + "\r\n\r\n"));
            return byteBuffer.ToArray();
        }

        public static byte[] EncodeMsg(byte type, byte[] msg)
        {
            ByteBuffer byteBuffer = new ByteBuffer();
            byteBuffer.Append(type);
            int length = msg.Length;
            if (length <= 125)
                byteBuffer.Append((byte)length);
            else if (length <= 16384)
            {
                byteBuffer.Append((byte)126);
                byteBuffer.Append((byte)(length >> 8 & (int)byte.MaxValue));
                byteBuffer.Append((byte)(length & (int)byte.MaxValue));
            }
            byteBuffer.Append(msg);
            return byteBuffer.ToArray();
        }

        public static byte[] DecodeMsg(byte[] msg)
        {
            uint num1 = (uint)msg[1] & (uint)sbyte.MaxValue;
            int sourceIndex = 2;
            switch (num1)
            {
                case 126:
                    sourceIndex = 4;
                    num1 = 0U;
                    for (int index = 2; index < sourceIndex; ++index)
                    {
                        byte num2 = msg[index];
                        num1 |= (uint)num2;
                        if (index < sourceIndex - 1)
                            num1 <<= 8;
                    }
                    break;
                case (uint)sbyte.MaxValue:
                    sourceIndex = 10;
                    num1 = 0U;
                    for (int index = 2; index < sourceIndex; ++index)
                    {
                        byte num2 = msg[index];
                        num1 |= (uint)num2;
                        if (index < sourceIndex - 1)
                            num1 <<= 8;
                    }
                    break;
            }
            byte[] numArray = new byte[4];
            Array.Copy((Array)msg, sourceIndex, (Array)numArray, 0, 4);
            int num3 = sourceIndex + 4;
            uint num4 = (uint)((ulong)num1 + (ulong)(sourceIndex + 4));
            ByteBuffer byteBuffer = new ByteBuffer();
            int index1 = num3;
            int num5 = 0;
            while ((long)index1 < (long)num4)
            {
                byte b = (byte)((uint)msg[index1] ^ (uint)numArray[num5 % 4]);
                byteBuffer.Append(b);
                ++index1;
                ++num5;
            }
            return byteBuffer.ToArray();
        }

        public static uint DecodeLength(byte[] msg)
        {
            uint num1 = 0;
            if (msg.Length > 0)
            {
                uint num2 = (uint)msg[1] & (uint)sbyte.MaxValue;
                int num3 = 2;
                switch (num2)
                {
                    case 126:
                        num3 = 4;
                        num2 = 0U;
                        for (int index = 2; index < num3; ++index)
                        {
                            byte num4 = msg[index];
                            num2 |= (uint)num4;
                            if (index < num3 - 1)
                                num2 <<= 8;
                        }
                        break;
                    case (uint)sbyte.MaxValue:
                        num3 = 10;
                        num2 = 0U;
                        for (int index = 2; index < num3; ++index)
                        {
                            byte num4 = msg[index];
                            num2 |= (uint)num4;
                            if (index < num3 - 1)
                                num2 <<= 8;
                        }
                        break;
                }
                num1 = (uint)((ulong)num2 + (ulong)(num3 + 4));
            }
            return num1;
        }
    }
}
