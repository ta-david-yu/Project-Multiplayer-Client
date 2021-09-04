using System;
using System.IO;

class Protocol
{
    private BinaryWriter m_Writer;
    private BinaryReader m_Reader;
    private MemoryStream m_Stream;
    private byte[] m_Buffer;

    public byte[] Serialize(byte code, uint value)
    {
        const int bufSize = sizeof(byte) + sizeof(int);
        initWriter(bufSize);
        m_Writer.Write(code);
        m_Writer.Write(value);
        return m_Buffer;
    }

    public byte[] Serialize(byte code, uint value, float x, float y, int facing)
    {
        const int bufSize = sizeof(byte) + sizeof(int) + sizeof(float) + sizeof(float) + sizeof(int);
        initWriter(bufSize);
        m_Writer.Write(code);
        m_Writer.Write(value);
        m_Writer.Write(x);
        m_Writer.Write(y);
        m_Writer.Write(facing);
        return m_Buffer;
    }

    public void Deserialize(byte[] buf, out byte code, out int value)
    {
        initReader(buf);
        m_Stream.Write(buf, 0, buf.Length);
        m_Stream.Position = 0;
        code = m_Reader.ReadByte();
        value = m_Reader.ReadInt32();
    }

    private void initWriter(int size)
    {
        m_Buffer = new byte[size];
        m_Stream = new MemoryStream(m_Buffer);
        m_Writer = new BinaryWriter(m_Stream);
    }

    private void initReader(byte[] buffer)
    {
        m_Stream = new MemoryStream(buffer);
        m_Reader = new BinaryReader(m_Stream);
    }
}