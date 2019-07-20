
/* Copyright (c) WebMathTraining Inc 2011. All rights reserved. */

using System;
using System.IO;

namespace BaseEntity.Database.Engine
{
  /// <summary>
  /// Handles a varbinary SQL type as a Stream.
  /// </summary>
  public class VarBinaryStream : Stream
  {
    private readonly VarBinarySource _source;
    private long _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="VarBinaryStream"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public VarBinaryStream(VarBinarySource source)
    {
      _position = 0;
      _source = source;
    }

    /// <summary>
    /// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
    /// </summary>
    /// <value></value>
    /// <returns>true if the stream supports reading; otherwise, false.
    /// </returns>
    public override bool CanRead
    {
      get { return true; }
    }

    /// <summary>
    /// When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
    /// </summary>
    /// <value></value>
    /// <returns>true if the stream supports seeking; otherwise, false.
    /// </returns>
    public override bool CanSeek
    {
      get { return true; }
    }

    /// <summary>
    /// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
    /// </summary>
    /// <value></value>
    /// <returns>true if the stream supports writing; otherwise, false.
    /// </returns>
    public override bool CanWrite
    {
      get { return true; }
    }

    /// <summary>
    /// When overridden in a derived class, gets the length in bytes of the stream.
    /// </summary>
    /// <value></value>
    /// <returns>
    /// A long value representing the length of the stream in bytes.
    /// </returns>
    /// <exception cref="T:System.NotSupportedException">
    /// A class derived from Stream does not support seeking.
    /// </exception>
    /// <exception cref="T:System.ObjectDisposedException">
    /// Methods were called after the stream was closed.
    /// </exception>
    public override long Length
    {
      get { return _source.Length == null ? 0 : _source.Length.Value; }
    }

    /// <summary>
    /// When overridden in a derived class, gets or sets the position within the current stream.
    /// </summary>
    /// <value></value>
    /// <returns>
    /// The current position within the stream.
    /// </returns>
    /// <exception cref="T:System.IO.IOException">
    /// An I/O error occurs.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">
    /// The stream does not support seeking.
    /// </exception>
    /// <exception cref="T:System.ObjectDisposedException">
    /// Methods were called after the stream was closed.
    /// </exception>
    public override long Position
    {
      get { return _position; }
      set { Seek(value, SeekOrigin.Begin); }
    }

    /// <summary>
    /// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    /// </summary>
    /// <exception cref="T:System.IO.IOException">
    /// An I/O error occurs.
    /// </exception>
    public override void Flush() {}

    /// <summary>
    /// When overridden in a derived class, sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>
    /// The new position within the current stream.
    /// </returns>
    /// <exception cref="T:System.IO.IOException">
    /// An I/O error occurs.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">
    /// The stream does not support seeking, such as if the stream is constructed from a pipe or console output.
    /// </exception>
    /// <exception cref="T:System.ObjectDisposedException">
    /// Methods were called after the stream was closed.
    /// </exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
      switch (origin)
      {
        case SeekOrigin.Begin:
          {
            if ((offset < 0) && (offset > Length))
            {
              throw new ArgumentException("Invalid seek origin.");
            }
            _position = offset;
            break;
          }
        case SeekOrigin.End:
          {
            if ((offset > 0) && (offset < -Length))
            {
              throw new ArgumentException("Invalid seek origin.");
            }
            _position = Length - offset;
            break;
          }
        case SeekOrigin.Current:
          {
            if ((_position + offset > Length) || (_position + offset < 0))
            {
              throw new ArgumentException("Invalid seek origin.");
            }
            _position = _position + offset;
            break;
          }
        default:
          {
            throw new ArgumentOutOfRangeException("origin", origin,
                                                  "Unknown SeekOrigin");
          }
      }
      return _position;
    }

    /// <summary>
    /// When overridden in a derived class, sets the length of the current stream.
    /// </summary>
    /// <param name="value">The desired length of the current stream in bytes.</param>
    /// <exception cref="T:System.IO.IOException">
    /// An I/O error occurs.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">
    /// The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output.
    /// </exception>
    /// <exception cref="T:System.ObjectDisposedException">
    /// Methods were called after the stream was closed.
    /// </exception>
    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    /// <summary>
    /// When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
    /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
    /// <returns>
    /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">
    /// The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length.
    /// </exception>
    /// <exception cref="T:System.ArgumentNullException">
    /// 	<paramref name="buffer"/> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="offset"/> or <paramref name="count"/> is negative.
    /// </exception>
    /// <exception cref="T:System.IO.IOException">
    /// An I/O error occurs.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">
    /// The stream does not support reading.
    /// </exception>
    /// <exception cref="T:System.ObjectDisposedException">
    /// Methods were called after the stream was closed.
    /// </exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
      if (buffer == null)
      {
        throw new ArgumentNullException("buffer");
      }
      if (offset < 0)
      {
        throw new ArgumentOutOfRangeException("offset");
      }
      if (count < 0)
      {
        throw new ArgumentOutOfRangeException("count");
      }
      if (buffer.Length - offset < count)
      {
        throw new ArgumentException("Offset and length were out of bounds for the array");
      }

      var data = _source.Read(Position, count);
      if (data == null)
      {
        return 0;
      }

      Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
      _position += data.Length;
      return data.Length;
    }

    /// <summary>
    /// When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</param>
    /// <param name="count">The number of bytes to be written to the current stream.</param>
    /// <exception cref="T:System.ArgumentException">
    /// The sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length.
    /// </exception>
    /// <exception cref="T:System.ArgumentNullException">
    /// 	<paramref name="buffer"/> is null.
    /// </exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// 	<paramref name="offset"/> or <paramref name="count"/> is negative.
    /// </exception>
    /// <exception cref="T:System.IO.IOException">
    /// An I/O error occurs.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">
    /// The stream does not support writing.
    /// </exception>
    /// <exception cref="T:System.ObjectDisposedException">
    /// Methods were called after the stream was closed.
    /// </exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
      if (buffer == null)
      {
        throw new ArgumentNullException("buffer");
      }
      if (offset < 0)
      {
        throw new ArgumentOutOfRangeException("offset");
      }
      if (count < 0)
      {
        throw new ArgumentOutOfRangeException("count");
      }
      if (buffer.Length - offset < count)
      {
        throw new ArgumentException("Offset and length were out of bounds for the array");
      }

      var data = GetWriteBuffer(buffer, count, offset);
      _source.Write(data, _position, count);
      _position += count;
    }

    private static byte[] GetWriteBuffer(byte[] buffer, int count, int offset)
    {
      if (buffer.Length == count)
      {
        return buffer;
      }
      var data = new byte[count];
      Buffer.BlockCopy(buffer, offset, data, 0, count);
      return data;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
      if (!disposing)
      {
        if (_source != null)
        {
          _source.Dispose();
        }
      }
      base.Dispose(disposing);
    }
  }
}