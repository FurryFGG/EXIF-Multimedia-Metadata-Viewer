namespace ExifMultimediaMetadataViewer.Services;

public sealed class BoundedLruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _items;
    private readonly LinkedList<(TKey Key, TValue Value)> _lru = new();
    private readonly object _lock = new();

    public BoundedLruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _items = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(comparer);
    }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (!_items.TryGetValue(key, out var node))
            {
                value = default!;
                return false;
            }
            _lru.Remove(node);
            _lru.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
    }

    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(key, out var existing))
            {
                existing.Value = (key, value);
                _lru.Remove(existing);
                _lru.AddFirst(existing);
                return;
            }
            var node = _lru.AddFirst((key, value));
            _items.Add(key, node);
            while (_items.Count > _capacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _items.Remove(last.Value.Key);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            _lru.Clear();
        }
    }
}
