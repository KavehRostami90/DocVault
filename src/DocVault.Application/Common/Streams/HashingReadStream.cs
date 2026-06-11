using System.Security.Cryptography;

namespace DocVault.Application.Common.Streams;

/// <summary>
/// Read-only pass-through stream that feeds every byte read from the inner stream into an
/// <see cref="IncrementalHash"/>, so a hash can be computed in the same pass that copies the
/// stream to its destination — without buffering the whole content in memory.
/// The hash is only complete once the consumer has read the inner stream to its end.
/// Does not take ownership of the inner stream or the hash; the caller disposes both.
/// </summary>
public sealed class HashingReadStream : Stream
{
  private readonly Stream _inner;
  private readonly IncrementalHash _hash;

  public HashingReadStream(Stream inner, IncrementalHash hash)
  {
    _inner = inner;
    _hash  = hash;
  }

  public override bool CanRead => true;
  public override bool CanSeek => false;
  public override bool CanWrite => false;
  public override long Length => _inner.Length;
  public override long Position
  {
    get => _inner.Position;
    set => throw new NotSupportedException();
  }

  public override int Read(byte[] buffer, int offset, int count)
  {
    var read = _inner.Read(buffer, offset, count);
    if (read > 0)
      _hash.AppendData(buffer, offset, read);
    return read;
  }

  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
  {
    var read = await _inner.ReadAsync(buffer, cancellationToken);
    if (read > 0)
      _hash.AppendData(buffer.Span[..read]);
    return read;
  }

  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

  public override void Flush() { }
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
  public override void SetLength(long value) => throw new NotSupportedException();
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
