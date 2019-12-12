// Decompiled with JetBrains decompiler
// Type: WebsocketServer.StringUtil
// Assembly: WebsocketServer, Version=1.0.0.27676, Culture=neutral, PublicKeyToken=null
// MVID: 9416C711-8AFD-444F-9A24-232C417E9E26
// Assembly location: C:\Users\artna\Desktop\WebsocketServer.dll

using System.Text;

namespace WebsocketServer
{
    public class StringUtil
    {
        public static string toString(byte[] bytes)
        {
            return Encoding.Default.GetString(bytes, 0, bytes.Length);
        }

        public static string toString(byte[] bytes, int count)
        {
            return Encoding.Default.GetString(bytes, 0, count);
        }

        public static byte[] toByteArray(string str)
        {
            return Encoding.Default.GetBytes(str);
        }

        public static string getBoundString(string msg, string startChar, string stopChar)
        {
            string str = "";
            if (msg != null && msg.Length > 0)
            {
                int num1 = msg.IndexOf(startChar);
                if (num1 >= 0)
                {
                    int startIndex = num1 + startChar.Length;
                    int num2 = msg.IndexOf(stopChar, startIndex + 1);
                    if (startIndex < num2)
                        str = msg.Substring(startIndex, num2 - startIndex);
                }
            }
            return str;
        }

        public static string toHexString(byte[] ba)
        {
            StringBuilder stringBuilder = new StringBuilder(ba.Length * 2);
            foreach (byte num in ba)
                stringBuilder.AppendFormat("{0:x2}", new object[1]
        {
          (object) num
        });
            return stringBuilder.ToString();
        }

        public static string toHexString(uint value)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("{0:x4}", new object[1]
      {
        (object) value
      });
            return stringBuilder.ToString();
        }

        public static string toHexString(byte[] ba, int count)
        {
            StringBuilder stringBuilder = new StringBuilder(count * 2);
            for (int index = 0; index < count; ++index)
                stringBuilder.AppendFormat("{0:x2}", new object[1]
        {
          (object) ba[index]
        });
            return stringBuilder.ToString();
        }

        public static string convertEncodingUnicode(string msg)
        {
            byte[] bytes = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, StringUtil.toByteArray(msg));
            return Encoding.Unicode.GetString(bytes, 0, bytes.Length);
        }

        public static string convertEncodingBigEndian(string msg)
        {
            byte[] bytes = Encoding.Convert(Encoding.UTF8, Encoding.BigEndianUnicode, StringUtil.toByteArray(msg));
            return Encoding.BigEndianUnicode.GetString(bytes, 0, bytes.Length);
        }

        public static string htmlEncoding(string msg)
        {
            msg = msg.Replace("\\n", "");
            msg = msg.Replace("<", "&lt;");
            msg = msg.Replace(">", "&gt;");
            msg = msg.Replace("\\\"", "\"");
            return msg;
        }

        public static string encode(string msg, ushort size)
        {
            string str = StringUtil.htmlEncoding(StringUtil.convertEncodingBigEndian(msg));
            if (str.Length > (int)size)
                str = str.Substring(0, (int)size);
            return str;
        }
    }
}
