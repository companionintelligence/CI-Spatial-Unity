using System;
using System.Collections.Generic;

namespace Companion.Spatial.Tracking
{
    /// <summary>
    /// A running vote: each sighting adds its score to the entry for its key, and the entry with the highest sum wins.
    /// A tie goes to the entry that was added first, so the winner never flips back and forth on equal scores. A track
    /// uses one vote for its tag and one for its label text. The number of entries is bounded: past the cap the weakest
    /// entry other than the one just voted for is dropped, so a stream of one-off spellings cannot grow without limit.
    /// </summary>
    internal sealed class ScoreVote
    {
        private readonly StringComparer _comparer;
        private readonly int _maxEntries;
        private readonly List<string> _keys = new List<string>();
        private readonly List<string> _display = new List<string>();
        private readonly List<double> _sums = new List<double>();

        internal ScoreVote(StringComparer comparer, int maxEntries)
        {
            _comparer = comparer;
            _maxEntries = maxEntries;
        }

        /// <summary>The number of entries held.</summary>
        internal int Count
        {
            get { return _keys.Count; }
        }

        /// <summary>The text shown for the entry with the highest sum (as first seen); null before any vote.</summary>
        internal string Winner
        {
            get
            {
                int best = -1;
                for (int i = 0; i < _sums.Count; i++)
                {
                    if (best < 0 || _sums[i] > _sums[best])
                    {
                        best = i;
                    }
                }

                return best < 0 ? null : _display[best];
            }
        }

        /// <summary>Adds <paramref name="score"/> to the entry for <paramref name="key"/>, making it if it is new; <paramref name="display"/> is kept from the first vote for the key.</summary>
        internal void Add(string key, string display, double score)
        {
            int index = -1;
            for (int i = 0; i < _keys.Count; i++)
            {
                if (_comparer.Equals(_keys[i], key))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                _keys.Add(key);
                _display.Add(display);
                _sums.Add(score);
                index = _keys.Count - 1;
            }
            else
            {
                _sums[index] += score;
            }

            if (_keys.Count > _maxEntries)
            {
                DropWeakestExcept(index);
            }
        }

        private void DropWeakestExcept(int keep)
        {
            int weakest = -1;
            for (int i = 0; i < _sums.Count; i++)
            {
                if (i == keep)
                {
                    continue;
                }

                // on a tie the later entry goes: the earlier one has been there longer
                if (weakest < 0 || _sums[i] <= _sums[weakest])
                {
                    weakest = i;
                }
            }

            _keys.RemoveAt(weakest);
            _display.RemoveAt(weakest);
            _sums.RemoveAt(weakest);
        }
    }
}
