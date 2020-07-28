// Decompiled with JetBrains decompiler
// Type: WebsocketServer.ByteBuffer
// Assembly: WebsocketServer, Version=1.0.0.27676, Culture=neutral, PublicKeyToken=null
// MVID: 9416C711-8AFD-444F-9A24-232C417E9E26
// Assembly location: C:\Users\artna\Desktop\WebsocketServer.dll

using System;
using System.Collections.Generic;

namespace WebsocketServer
{
  public class ByteBuffer
  {
    private List<byte> buffer;

    public ByteBuffer()
    {
      this.buffer = new List<byte>();
    }

    public void Append(byte[] bytes)
    {
      this.buffer.AddRange((IEnumerable<byte>) bytes);
    }

    public void Append(byte[] bytes, int count)
    {
      this.buffer.AddRange((IEnumerable<byte>) this.getBytes(bytes, count));
    }

    public void Append(byte b)
    {
      this.buffer.Add(b);
    }

    public void Clear()
    {
      this.buffer.Clear();
    }

    public int Length()
    {
      return this.buffer.Count;
    }

    public int IndexOf(byte search)
    {
      return this.buffer.IndexOf(search);
    }

    public int IndexOf(string search)
    {
      return this.IndexOf(this.getBytes(search));
    }

    public int IndexOf(byte[] search)
    {
      byte[] array = this.buffer.ToArray();
      for (int position = 0; position < this.buffer.Count; ++position)
      {
        if (this.isMatch(array, position, search))
          return position;
      }
      return -1;
    }

    public ByteBuffer SubByteBuffer(int startIndex, int count)
    {
      ByteBuffer byteBuffer = new ByteBuffer();
      byteBuffer.Append(this.buffer.GetRange(startIndex, count).ToArray());
      return byteBuffer;
    }

    public byte[] ToArray(int startIndex, int count)
    {
      return this.buffer.GetRange(startIndex, count).ToArray();
    }

    public byte[] ToArray()
    {
      return this.buffer.ToArray();
    }

    public string Substring(int startIndex, int count)
    {
      return this.ToString().Substring(startIndex, count);
    }

    public override string ToString()
    {
      return StringUtil.toString(this.buffer.ToArray());
    }

    public void Delete(int startIndex, int count)
    {
      this.buffer.RemoveRange(startIndex, count);
    }

    private byte[] getBytes(string str)
    {
      return StringUtil.toByteArray(str);
    }

    private byte[] getBytes(byte[] array, int count)
    {
      byte[] numArray = new byte[count];
      Buffer.BlockCopy((Array) array, 0, (Array) numArray, 0, count);
      return numArray;
    }

    private bool isMatch(byte[] array, int position, byte[] candidate)
    {
      if (candidate.Length > array.Length - position)
        return false;
      for (int index = 0; index < candidate.Length; ++index)
      {
        if ((int) array[position + index] != (int) candidate[index])
          return false;
      }
      return true;
    }
  }
}
