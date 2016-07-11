using System;
using System.IO;

namespace IrcDotNet
{
    // Allows reading and writing to circular buffer as stream.
    // Note: Stream is non-blocking and non-thread-safe.
    internal class CircularBufferStream : Stream
    {
        // Buffer for storing data.
        private long readPosition;

        // Current index within buffer for writing and reading.
        private long writePosition;

        public CircularBufferStream(int length)
            : this(new byte[length])
        {
        }

        public CircularBufferStream(byte[] buffer)
        {
            Buffer = buffer;
            writePosition = 0;
            readPosition = 0;
        }

        public byte[] Buffer { get; }

        public long WritePosition
        {
            get { return writePosition; }
            set { writePosition = value%Buffer.Length; }
        }

        public override long Position
        {
            get { return readPosition; }
            set { readPosition = value%Buffer.Length; }
        }

        public override long Length
        {
            get
            {
                var length = writePosition - readPosition;
                return length < 0 ? Buffer.Length + length : length;
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
                    readPosition = offset%Buffer.Length;
                    break;
                case SeekOrigin.End:
                    readPosition = (Buffer.Length - offset)%Buffer.Length;
                    break;
                case SeekOrigin.Current:
                    readPosition = (readPosition + offset)%Buffer.Length;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return readPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Write block of bytes from given buffer into circular buffer, wrapping around when necessary.
            int writeCount;
            while ((writeCount = Math.Min(count, (int) (Buffer.Length - writePosition))) > 0)
            {
                var oldWritePosition = writePosition;
                var newWritePosition = (writePosition + writeCount)%Buffer.Length;
                if (newWritePosition > readPosition && oldWritePosition < readPosition)
                {
#if !SILVERLIGHT && !NETSTANDARD1_5
                    throw new InternalBufferOverflowException("The CircularBuffer was overflowed!");
#else
                    throw new IOException("The CircularBuffer was overflowed!");
#endif
                }
                System.Buffer.BlockCopy(buffer, offset, Buffer, (int) writePosition, writeCount);
                writePosition = newWritePosition;

                offset += writeCount;
                count -= writeCount; //writeCount <= count => now is count >=0
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Read block of bytes from circular buffer, wrapping around when necessary.
            var totalReadCount = 0;
            int readCount;
            count = Math.Min(buffer.Length - offset, count);
            while ((readCount = Math.Min(count, (int) Length)) > 0)
            {
                if (readCount > Buffer.Length - readPosition)
                {
                    readCount = (int) (Buffer.Length - readPosition);
                }
                System.Buffer.BlockCopy(Buffer, (int) readPosition, buffer, offset, readCount);
                readPosition = (readPosition + readCount)%Buffer.Length;
                offset += readCount;
                count = Math.Min(buffer.Length - offset, count);
                totalReadCount += readCount;
            }
            return totalReadCount;
        }
    }
}