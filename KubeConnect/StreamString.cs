using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class StreamString
    {
        private Stream ioStream;
        private UnicodeEncoding streamEncoding;
        public byte[] readBuffer = new byte[2];
        public StreamString(Stream ioStream)
        {
            this.ioStream = ioStream;
            streamEncoding = new UnicodeEncoding();
        }

        public (string type, string message) ReadString()
        {
            int len = ioStream.ReadByte() * 256;
            len += ioStream.ReadByte();

            if (len < 0)
            {
                throw new TaskCanceledException();
            }

            var inBuffer = new byte[len];
            ioStream.Read(inBuffer, 0, len);

            var str = streamEncoding.GetString(inBuffer);
            var idx = str.IndexOf('@');
            var type = str.Substring(0, idx);
            var msg = str.Substring(idx + 1);
            return (type, msg);
        }

        public int WriteString(string type, string message)
        {
            var finalString = type + '@' + message;
            byte[] outBuffer = streamEncoding.GetBytes(finalString);
            int len = outBuffer.Length;
            if (len > UInt16.MaxValue)
            {
                len = (int)UInt16.MaxValue;
            }
            ioStream.WriteByte((byte)(len / 256));
            ioStream.WriteByte((byte)(len & 255));
            ioStream.Write(outBuffer, 0, len);
            ioStream.Flush();

            return outBuffer.Length + 2;
        }
    }
}
