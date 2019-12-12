// Decompiled with JetBrains decompiler
// Type: WebsocketServer.SHA1Util
// Assembly: WebsocketServer, Version=1.0.0.27676, Culture=neutral, PublicKeyToken=null
// MVID: 9416C711-8AFD-444F-9A24-232C417E9E26
// Assembly location: C:\Users\artna\Desktop\WebsocketServer.dll

using System;
using System.Collections.Generic;

namespace WebsocketServer
{
    public class SHA1Util
    {
        private const ushort MAX_CHUNK = 64;
        private const ushort RESULT_SIZE = 5;

        private uint rotateLeft(uint value, ushort shift)
        {
            return value << (int)shift | value >> 32 - (int)shift;
        }

        public byte[] Hash(byte[] data)
        {
            ByteBuffer byteBuffer1 = new ByteBuffer();
            byteBuffer1.Append(data);
            byteBuffer1.Append((byte)128);
            while (byteBuffer1.Length() * 8 % 512 != 448)
                byteBuffer1.Append((byte)0);
            ulong num1 = (ulong)(data.Length * 8);
            Stack<byte> byteStack1 = new Stack<byte>();
            for (byte index = 0; index < (byte)8; ++index)
            {
                byte num2 = (byte)(num1 % 256UL);
                byteStack1.Push(num2);
                num1 = (num1 - (ulong)num2) / 256UL;
            }
            byte[] array1 = byteStack1.ToArray();
            byteBuffer1.Append(array1);
            uint[] numArray1 = new uint[5]
      {
        1732584193U,
        4023233417U,
        2562383102U,
        271733878U,
        3285377520U
      };
            uint[] numArray2 = new uint[80];
            Array.Clear((Array)numArray2, 0, numArray2.Length);
            while (byteBuffer1.Length() > 0)
            {
                ByteBuffer byteBuffer2 = byteBuffer1.SubByteBuffer(0, 64);
                byteBuffer1.Delete(0, 64);
                for (int index = 0; index < 16; ++index)
                {
                    byte[] array2 = byteBuffer2.ToArray(0, 4);
                    byteBuffer2.Delete(0, 4);
                    numArray2[index] = (uint)((int)array2[0] << 24 | (int)array2[1] << 16 | (int)array2[2] << 8) | (uint)array2[3];
                }
                for (byte index = 16; index < (byte)80; ++index)
                    numArray2[(int)index] = this.rotateLeft(numArray2[(int)index - 3] ^ numArray2[(int)index - 8] ^ numArray2[(int)index - 14] ^ numArray2[(int)index - 16], (ushort)1);
                uint num2 = numArray1[0];
                uint num3 = numArray1[1];
                uint num4 = numArray1[2];
                uint num5 = numArray1[3];
                uint num6 = numArray1[4];
                uint num7 = 0;
                uint num8 = 0;
                for (int index = 0; index < 80; ++index)
                {
                    if (index >= 0 && index <= 19)
                    {
                        num7 = (uint)((int)num3 & (int)num4 | ~(int)num3 & (int)num5);
                        num8 = 1518500249U;
                    }
                    else if (index >= 20 && index <= 39)
                    {
                        num7 = num3 ^ num4 ^ num5;
                        num8 = 1859775393U;
                    }
                    else if (index >= 40 && index <= 59)
                    {
                        num7 = (uint)((int)num3 & (int)num4 | (int)num3 & (int)num5 | (int)num4 & (int)num5);
                        num8 = 2400959708U;
                    }
                    else if (index >= 60 && index <= 79)
                    {
                        num7 = num3 ^ num4 ^ num5;
                        num8 = 3395469782U;
                    }
                    uint num9 = this.rotateLeft(num2, (ushort)5) + num7 + num6 + num8 + numArray2[index];
                    num6 = num5;
                    num5 = num4;
                    num4 = this.rotateLeft(num3, (ushort)30);
                    num3 = num2;
                    num2 = num9;
                }
                numArray1[0] += num2;
                numArray1[1] += num3;
                numArray1[2] += num4;
                numArray1[3] += num5;
                numArray1[4] += num6;
            }
            Stack<byte> byteStack2 = new Stack<byte>();
            for (int index1 = 4; index1 >= 0; --index1)
            {
                uint num2 = numArray1[index1];
                for (int index2 = 0; index2 < 4; ++index2)
                {
                    byte num3 = (byte)(num2 % 256U);
                    byteStack2.Push(num3);
                    num2 = (num2 - (uint)num3) / 256U;
                }
            }
            return byteStack2.ToArray();
        }
    }
}
