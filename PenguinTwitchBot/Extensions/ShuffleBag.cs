using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace PenguinTwitchBot.Extensions
{
    /// <summary>
    /// A standalone Shuffle Bag (Deck) randomizer.
    /// Draws each item exactly once before refilling and reshuffling.
    /// Guarantees that across refills, the first item drawn does not repeat the last item drawn (if Count > 1).
    /// </summary>
    /// <typeparam name="T">The type of item stored in the bag.</typeparam>
    public class ShuffleBag<T>
    {
        private readonly List<T> _sourceItems = [];
        private readonly List<T> _remaining = [];
        private readonly IEqualityComparer<T> _comparer;
        private readonly object _lock = new();
        private T? _lastDrawn;
        private bool _hasLastDrawn;

        public ShuffleBag(IEqualityComparer<T>? comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public ShuffleBag(IEnumerable<T> items, IEqualityComparer<T>? comparer = null)
            : this(comparer)
        {
            SetItems(items);
        }

        public int RemainingCount
        {
            get
            {
                lock (_lock) return _remaining.Count;
            }
        }

        public int TotalCount
        {
            get
            {
                lock (_lock) return _sourceItems.Count;
            }
        }

        public void SetItems(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            lock (_lock)
            {
                _sourceItems.Clear();
                _sourceItems.AddRange(items);
                ResetBagUnsafe();
            }
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                _sourceItems.Add(item);
                _remaining.Add(item);
                // Shuffle newly added item into remaining
                if (_remaining.Count > 1)
                {
                    int swapIdx = RandomNumberGenerator.GetInt32(_remaining.Count);
                    (_remaining[^1], _remaining[swapIdx]) = (_remaining[swapIdx], _remaining[^1]);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _sourceItems.Clear();
                _remaining.Clear();
                _lastDrawn = default;
                _hasLastDrawn = false;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                ResetBagUnsafe();
            }
        }

        public T? Next()
        {
            lock (_lock)
            {
                if (_sourceItems.Count == 0)
                {
                    return default;
                }

                if (_sourceItems.Count == 1)
                {
                    _lastDrawn = _sourceItems[0];
                    _hasLastDrawn = true;
                    return _lastDrawn;
                }

                if (_remaining.Count == 0)
                {
                    RefillAndShuffleUnsafe();
                }

                var item = _remaining[^1];
                _remaining.RemoveAt(_remaining.Count - 1);
                _lastDrawn = item;
                _hasLastDrawn = true;
                return item;
            }
        }

        private void ResetBagUnsafe()
        {
            _remaining.Clear();
            if (_sourceItems.Count == 0) return;
            RefillAndShuffleUnsafe();
        }

        private void RefillAndShuffleUnsafe()
        {
            _remaining.Clear();
            _remaining.AddRange(_sourceItems);

            // Fisher-Yates shuffle using cryptographically secure RNG
            for (int i = _remaining.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (_remaining[i], _remaining[j]) = (_remaining[j], _remaining[i]);
            }

            // Anti-streak rule across refills:
            // If the next item to draw (_remaining[^1]) matches the last item drawn, swap it.
            if (_remaining.Count > 1 && _hasLastDrawn && _comparer.Equals(_remaining[^1], _lastDrawn!))
            {
                int swapIdx = RandomNumberGenerator.GetInt32(_remaining.Count - 1);
                (_remaining[^1], _remaining[swapIdx]) = (_remaining[swapIdx], _remaining[^1]);
            }
        }
    }

    /// <summary>
    /// Global registry and helpers for keyed shuffle bags.
    /// Provides thread-safe, non-repeating fair random selection for actions, timers, minigames, and more.
    /// Stores keys rather than object references to prevent memory leaks with EF Core entities.
    /// </summary>
    public static class ShuffleBag
    {
        private static readonly ConcurrentDictionary<string, KeyedBagRunner> _bags = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Draws the next item from the shuffle bag associated with <paramref name="bagKey"/>.
        /// Identifies items by <paramref name="keySelector"/>.
        /// </summary>
        public static T? Next<T, TKey>(string bagKey, IEnumerable<T> candidates, Func<T, TKey> keySelector) where TKey : notnull
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bagKey);
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(keySelector);

            var runner = _bags.GetOrAdd(bagKey, _ => new KeyedBagRunner());
            return runner.Next(candidates, keySelector);
        }

        /// <summary>
        /// Draws the next item from the shuffle bag associated with <paramref name="bagKey"/> using default equality.
        /// </summary>
        public static T? Next<T>(string bagKey, IEnumerable<T> candidates, IEqualityComparer<T>? comparer = null) where T : notnull
        {
            return Next(bagKey, candidates, x => x);
        }

        /// <summary>
        /// Resets or removes the shuffle bag for the specified key.
        /// </summary>
        public static void Reset(string bagKey)
        {
            if (!string.IsNullOrWhiteSpace(bagKey))
            {
                _bags.TryRemove(bagKey, out _);
            }
        }

        /// <summary>
        /// Gets the remaining number of items in the keyed shuffle bag before the next refill.
        /// </summary>
        public static int GetRemainingCount(string bagKey)
        {
            if (string.IsNullOrWhiteSpace(bagKey)) return 0;
            return _bags.TryGetValue(bagKey, out var runner) ? runner.RemainingCount : 0;
        }

        /// <summary>
        /// Gets the total number of candidate items in the keyed shuffle bag.
        /// </summary>
        public static int GetTotalCount(string bagKey)
        {
            if (string.IsNullOrWhiteSpace(bagKey)) return 0;
            return _bags.TryGetValue(bagKey, out var runner) ? runner.TotalCount : 0;
        }

        /// <summary>
        /// Synchronizes the candidates in the keyed shuffle bag without drawing an item.
        /// Useful when initializing or refreshing lists (such as playlists) to update remaining counts.
        /// </summary>
        public static void Sync<T, TKey>(string bagKey, IEnumerable<T> candidates, Func<T, TKey> keySelector) where TKey : notnull
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(bagKey);
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(keySelector);

            var runner = _bags.GetOrAdd(bagKey, _ => new KeyedBagRunner());
            runner.Sync(candidates, keySelector);
        }

        /// <summary>
        /// Clears all global shuffle bags.
        /// </summary>
        public static void ClearAll()
        {
            _bags.Clear();
        }

        private sealed class KeyedBagRunner
        {
            private readonly object _lock = new();
            private readonly List<object> _sourceKeys = [];
            private readonly List<object> _remainingKeys = [];
            private object? _lastKey;
            private bool _hasLastKey;

            public int RemainingCount
            {
                get
                {
                    lock (_lock) return _remainingKeys.Count;
                }
            }

            public int TotalCount
            {
                get
                {
                    lock (_lock) return _sourceKeys.Count;
                }
            }

            public void Sync<T, TKey>(IEnumerable<T> candidates, Func<T, TKey> keySelector) where TKey : notnull
            {
                var candidateList = candidates as IList<T> ?? candidates.ToList();
                lock (_lock)
                {
                    SyncInternal(candidateList, keySelector);
                }
            }

            public T? Next<T, TKey>(IEnumerable<T> candidates, Func<T, TKey> keySelector) where TKey : notnull
            {
                var candidateList = candidates as IList<T> ?? candidates.ToList();
                if (candidateList.Count == 0) return default;
                if (candidateList.Count == 1) return candidateList[0];

                lock (_lock)
                {
                    SyncInternal(candidateList, keySelector);

                    if (_remainingKeys.Count == 0)
                    {
                        RefillAndShuffleUnsafe();
                    }

                    var selectedKey = _remainingKeys[^1];
                    _remainingKeys.RemoveAt(_remainingKeys.Count - 1);
                    _lastKey = selectedKey;
                    _hasLastKey = true;

                    // Match item in current candidates
                    var match = candidateList.FirstOrDefault(c => Equals(keySelector(c), selectedKey));
                    return match is not null ? match : candidateList[0];
                }
            }

            private void SyncInternal<T, TKey>(IList<T> candidateList, Func<T, TKey> keySelector) where TKey : notnull
            {
                var currentKeyList = candidateList.Select(c => (object)keySelector(c)).ToList();

                // Synchronize bag if the candidates changed
                if (!AreKeySetsEquivalent(_sourceKeys, currentKeyList))
                {
                    var newKeys = currentKeyList.Where(ck => !_sourceKeys.Any(sk => Equals(sk, ck))).ToList();

                    _sourceKeys.Clear();
                    _sourceKeys.AddRange(currentKeyList);

                    // Retain any remaining keys that still exist in current candidates
                    var validRemaining = _remainingKeys.Where(k => currentKeyList.Any(ck => Equals(ck, k))).ToList();

                    // Shuffle any newly added keys into the active remaining deck
                    foreach (var newKey in newKeys)
                    {
                        validRemaining.Add(newKey);
                        if (validRemaining.Count > 1)
                        {
                            int swapIdx = RandomNumberGenerator.GetInt32(validRemaining.Count);
                            (validRemaining[^1], validRemaining[swapIdx]) = (validRemaining[swapIdx], validRemaining[^1]);
                        }
                    }

                    _remainingKeys.Clear();
                    _remainingKeys.AddRange(validRemaining);
                }

                if (_remainingKeys.Count == 0 && _sourceKeys.Count > 0)
                {
                    RefillAndShuffleUnsafe();
                }
            }

            private void RefillAndShuffleUnsafe()
            {
                _remainingKeys.Clear();
                _remainingKeys.AddRange(_sourceKeys);

                for (int i = _remainingKeys.Count - 1; i > 0; i--)
                {
                    int j = RandomNumberGenerator.GetInt32(i + 1);
                    (_remainingKeys[i], _remainingKeys[j]) = (_remainingKeys[j], _remainingKeys[i]);
                }

                if (_remainingKeys.Count > 1 && _hasLastKey && Equals(_remainingKeys[^1], _lastKey))
                {
                    int swapIdx = RandomNumberGenerator.GetInt32(_remainingKeys.Count - 1);
                    (_remainingKeys[^1], _remainingKeys[swapIdx]) = (_remainingKeys[swapIdx], _remainingKeys[^1]);
                }
            }

            private static bool AreKeySetsEquivalent(List<object> a, List<object> b)
            {
                if (a.Count != b.Count) return false;
                for (int i = 0; i < a.Count; i++)
                {
                    if (!Equals(a[i], b[i])) return false;
                }
                return true;
            }
        }
    }
}

