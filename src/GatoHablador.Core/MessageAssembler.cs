namespace GatoHablador.Core;

public sealed class MessageAssembler
{
    private readonly Dictionary<byte, PendingMessage> _pending = new();
    private readonly HashSet<byte> _completed = new();

    public bool TryAdd(BridgeChunk chunk, out byte[] payload)
    {
        payload = [];
        if (_completed.Contains(chunk.Sequence))
        {
            return false;
        }

        if (!_pending.TryGetValue(chunk.Sequence, out var pending) || pending.ChunkCount != chunk.Count)
        {
            pending = new PendingMessage(chunk.Count);
            _pending[chunk.Sequence] = pending;
        }

        pending.Set(chunk.Index, chunk.Payload);
        RemoveExpired();
        if (!pending.IsComplete)
        {
            return false;
        }

        payload = pending.Join();
        _pending.Remove(chunk.Sequence);
        _completed.Add(chunk.Sequence);
        if (_completed.Count > 128)
        {
            _completed.Clear();
            _completed.Add(chunk.Sequence);
        }

        return true;
    }

    public void Reset()
    {
        _pending.Clear();
        _completed.Clear();
    }

    private void RemoveExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(10);
        foreach (var sequence in _pending.Where(item => item.Value.LastUpdated < cutoff).Select(item => item.Key).ToArray())
        {
            _pending.Remove(sequence);
        }
    }

    private sealed class PendingMessage(byte chunkCount)
    {
        private readonly byte[]?[] _chunks = new byte[chunkCount][];

        public byte ChunkCount { get; } = chunkCount;
        public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.UtcNow;
        public bool IsComplete => _chunks.All(item => item is not null);

        public void Set(byte index, byte[] payload)
        {
            _chunks[index] ??= payload;
            LastUpdated = DateTimeOffset.UtcNow;
        }

        public byte[] Join()
        {
            var size = _chunks.Sum(item => item?.Length ?? 0);
            var result = new byte[size];
            var offset = 0;
            foreach (var chunk in _chunks)
            {
                if (chunk is null)
                {
                    throw new InvalidOperationException("The message is incomplete.");
                }

                chunk.CopyTo(result, offset);
                offset += chunk.Length;
            }

            return result;
        }
    }
}

