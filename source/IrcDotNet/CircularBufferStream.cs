using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace IrcDotNet
{
    // Allows reading and writing to circular buffer as stream.
    // Note: Stream is non-blocking and non-thread-safe.
    internal class CircularBufferStream : Stream
    {
        // Buffer for storing data.
        private byte[] buffer;

        // Current index within buffer for writing and reading.
        private long writePosition;
        private long readPosition;

        public CircularBufferStream(int length)
            : this(new byte[length])
        {
        }

        public CircularBufferStream(byte[] buffer)
        {
            this.buffer = buffer;
            this.writePosition = 0;
            this.readPosition = 0;
        }

        public byte[] Buffer
        {
            get { return this.buffer; }
        }

        public long WritePosition
        {
            get { return this.writePosition; }
            set { this.writePosition = value % this.buffer.Length; }
        }

        public override long Position
        {
            get { return this.readPosition; }
            set { this.readPosition = value % this.buffer.Length; }
        }

        public override long Length
        {
            get
            {
                var length = this.writePosition - this.readPosition;
                return length < 0 ? this.buffer.Length + length : length;
            }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override void Flush()
        {
            //
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    this.readPosition = offset % this.buffer.Length;
                    break;
                case SeekOrigin.End:
                    this.readPosition = (this.buffer.Length - offset) % this.buffer.Length;
                    break;
                case SeekOrigin.Current:
                    this.readPosition = (this.readPosition + offset) % this.buffer.Length;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return this.readPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Write block of bytes from given buffer into circular buffer, wrapping around when necessary.
            int writeCount;
            while ((writeCount = Math.Min(count, (int)(this.buffer.Length - this.writePosition))) > 0)
            {
                var oldWritePosition = this.writePosition;
                var newWritePosition = (this.writePosition + writeCount) % this.buffer.Length;
                if (newWritePosition > readPosition && oldWritePosition < readPosition)
                {
#if !SILVERLIGHT
                    throw new InternalBufferOverflowException("The CircularBuffer was overflowed!");
#else
                    throw new IOException("The CircularBuffer was overflowed!");
#endif
                }
                System.Buffer.BlockCopy(buffer, offset, this.buffer, (int)this.writePosition, writeCount);
                this.writePosition = newWritePosition;
               
                offset += writeCount;
                count -= writeCount; //writeCount <= count => now is count >=0
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Read block of bytes from circular buffer, wrapping around when necessary.
            int totalReadCount = 0;
            int readCount;
            count = Math.Min(buffer.Length - offset, count);
            while ((readCount = Math.Min(count, (int)(Length))) > 0)
            {
                if (readCount > this.buffer.Length - readPosition)
                {
                    readCount = (int)(this.buffer.Length - readPosition);
                }
                System.Buffer.BlockCopy(this.buffer, (int)this.readPosition, buffer, offset, readCount);
                this.readPosition = (this.readPosition + readCount) % this.buffer.Length;
                offset += readCount;
                count = Math.Min(buffer.Length - offset, count);
                totalReadCount += readCount;
            }
            return totalReadCount;
        }
    }
}
