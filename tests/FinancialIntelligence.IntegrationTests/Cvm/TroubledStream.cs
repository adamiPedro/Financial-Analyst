namespace FinancialIntelligence.IntegrationTests.Cvm;

/// <summary>
/// A response body that delivers its first bytes and then does whatever
/// <paramref name="afterHead"/> says: throw (a dropped connection) or wait
/// forever (a stalled one).
/// </summary>
internal sealed class TroubledStream(byte[] head, Func<CancellationToken, Task<int>> afterHead) : Stream
{
    private int _position;

    public static TroubledStream Dropping(byte[] head) =>
        new(head, _ => Task.FromException<int>(new IOException("Connection reset by peer.")));

    public static TroubledStream Stalling(byte[] head, Action? onStall = null) =>
        new(head, async cancellationToken =>
        {
            onStall?.Invoke();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        });

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position < head.Length)
        {
            var count = Math.Min(buffer.Length, head.Length - _position);
            head.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        return await afterHead(cancellationToken);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
