using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


namespace OpenRA.Benchmark.PriorityQueue
{
	public interface IPriorityQueue<TPriority, TValue>
	where TPriority : struct, IEquatable<TPriority>, IComparable<TPriority>
	{
		bool Any();
		int Count { get; }
		void Enqueue(TPriority priority, TValue value);
		void Enqueue(HexKeyValuePair<TPriority, TValue> item);
		bool TryDequeue(out HexKeyValuePair<TPriority, TValue> result);
		bool TryPeek(out HexKeyValuePair<TPriority, TValue> result);
	}

	public struct HexKeyValuePair<TKey, TValue> : IEquatable<HexKeyValuePair<TKey, TValue>>, IComparable<HexKeyValuePair<TKey, TValue>>
	where TKey : struct, IEquatable<TKey>, IComparable<TKey>
	{

		internal HexKeyValuePair(TKey key, TValue value) : this()
		{
			Key = key;
			Value = value;
		}

		public TKey Key { get; private set; }

		public TValue Value { get; private set; }

		/// <inheritdoc/>
		public override bool Equals(object obj)
		{
			var other = obj as HexKeyValuePair<TKey, TValue>?;
			return other.HasValue && this == other.Value;
		}

		/// <inheritdoc/>
		public override int GetHashCode() { return Key.GetHashCode(); }

		/// <inheritdoc/>
		public bool Equals(HexKeyValuePair<TKey, TValue> other) { return this == other; }

		/// <summary>Tests value-inequality.</summary>
		public static bool operator !=(HexKeyValuePair<TKey, TValue> lhs, HexKeyValuePair<TKey, TValue> rhs)
		{
			return lhs.CompareTo(rhs) != 0;
		}

		/// <summary>Tests value-equality.</summary>
		public static bool operator ==(HexKeyValuePair<TKey, TValue> lhs, HexKeyValuePair<TKey, TValue> rhs)
		{
			return lhs.CompareTo(rhs) == 0;
		}


		/// <summary>Tests whether lhs &lt; rhs.</summary>
		public static bool operator <(HexKeyValuePair<TKey, TValue> lhs, HexKeyValuePair<TKey, TValue> rhs)
		{
			return lhs.CompareTo(rhs) < 0; ;
		}
		/// <summary>Tests whether lhs &lt;= rhs.</summary>
		public static bool operator <=(HexKeyValuePair<TKey, TValue> lhs, HexKeyValuePair<TKey, TValue> rhs)
		{
			return lhs.CompareTo(rhs) <= 0; ;
		}
		/// <summary>Tests whether lhs &gt;= rhs.</summary>
		public static bool operator >=(HexKeyValuePair<TKey, TValue> lhs, HexKeyValuePair<TKey, TValue> rhs)
		{
			return lhs.CompareTo(rhs) >= 0;
		}
		/// <summary>Tests whether lhs &gt; rhs.</summary>
		public static bool operator >(HexKeyValuePair<TKey, TValue> lhs, HexKeyValuePair<TKey, TValue> rhs)
		{
			return lhs.CompareTo(rhs) > 0;
		}
		/// <inheritdoc/>
		public int CompareTo(HexKeyValuePair<TKey, TValue> other) { return this.Key.CompareTo(other.Key); }

	}

	internal class HotPriorityQueueList<TKey, TValue>: ICollection<HexKeyValuePair<TKey, TValue>>
		where TKey : struct, IEquatable<TKey>, IComparable<TKey>
	{

		public HotPriorityQueueList() : this(1024) { }

		public HotPriorityQueueList(int capacity)
		{
			_list = new List<HexKeyValuePair<TKey, TValue>>(capacity);
		}


		public int Count { get { return _list.Count; } }


		public bool IsReadOnly { get { return false; } }


		public void Add(HexKeyValuePair<TKey, TValue> item) { _list.Add(item); }

		public IPriorityQueue<TKey, TValue> PriorityQueue
		{
			get { return new MinListHeap(ref _list); }
		}


		public IEnumerator<HexKeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return _list.GetEnumerator();
		}


		public void Clear() { _list.Clear(); }


		public bool Contains(HexKeyValuePair<TKey, TValue> item)
		{
			return _list.Contains(item);
		}


		public void CopyTo(HexKeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			_list.CopyTo(array, arrayIndex);
		}


		public bool Remove(HexKeyValuePair<TKey, TValue> item)
		{
			throw new InvalidOperationException("Remove");
		}

		IEnumerator IEnumerable.GetEnumerator() { return _list.GetEnumerator(); }

		List<HexKeyValuePair<TKey, TValue>> _list;  // < backing store


		private class MinListHeap : IPriorityQueue<TKey, TValue>
		{
	

			public MinListHeap() : this(16) { }


			public MinListHeap(int capacity)
			{
				_items = new List<HexKeyValuePair<TKey, TValue>>(capacity);
			}

			public MinListHeap(ref List<HexKeyValuePair<TKey, TValue>> list)
			{
				if (list == null) throw new ArgumentNullException("list");

				_items = list;
				for (var start = (_items.Count - 1) / 2; start >= 0; start--) MinHeapifyDown(start);
				list = null;
			}

			public bool Any { get { return _items.Count > 0; } }


			public int Count { get { return _items.Count; } }


			bool IPriorityQueue<TKey, TValue>.Any() { return Any; }


			public void Clear() { _items.Clear(); }

	
			public void Enqueue(TKey key, TValue value)
			{
				Enqueue(new HexKeyValuePair<TKey, TValue>(key, value));
			}


			public void Enqueue(HexKeyValuePair<TKey, TValue> item)
			{
				_items.Add(item);
				var child = Count - 1;
				var parent = (child - 1) / 2;

				while (child > 0 && _items[parent] > _items[child])
				{
					var heap = _items[parent]; _items[parent] = _items[child]; _items[child] = heap;
					child = parent;
					parent = (child - 1) / 2;
				}
			}

			public bool TryDequeue(out HexKeyValuePair<TKey, TValue> result)
			{
				if (_items.Count == 0)
				{
					result = default(HexKeyValuePair<TKey, TValue>);
					return false;
				}

				result = _items[0];

				// Remove the first item if neighbour will only be 0 or 1 items left after doing so.  
				if (_items.Count <= 2)
					_items.RemoveAt(0);
				else
				{
					// Remove the first item and move the last item to the front.
					_items[0] = _items[_items.Count - 1];
					_items.RemoveAt(_items.Count - 1);

					MinHeapifyDown(0);
				}
				return true;
			}


			public bool TryPeek(out HexKeyValuePair<TKey, TValue> result)
			{
				if (_items.Count == 0)
				{
					result = default(HexKeyValuePair<TKey, TValue>);
					return false;
				}

				result = _items[0];
				return true;
			}

			private List<HexKeyValuePair<TKey, TValue>> _items;  

	
			private void MinHeapifyDown(int current)
			{

				int leftChild;
				while ((leftChild = 2 * current + 1) < _items.Count)
				{

					// identify smallest of parent and both children
					var smallest = _items[leftChild] < _items[current] ? leftChild
																		 : current;
					var rightChild = leftChild + 1;
					if (rightChild < _items.Count && _items[rightChild] < _items[smallest])
						smallest = rightChild;

					// if nothing to swap, ... then the tree is a heap
					if (current == smallest) break;

					// swap smallest value up
					var temp = _items[current];
					_items[current] = _items[smallest];
					_items[smallest] = temp;

					// follow swapped value down and repeat until the tree is a heap
					current = smallest;
				}
			}
		}
	}

	public class HotPriorityQueue<TValue> : IPriorityQueue<short, TValue>
	{

		short _baseIndex;
		short _preferenceWidth;
		IPriorityQueue<short, TValue> _queue;
		IDictionary<short, HotPriorityQueueList<short, TValue>> _lists;


		public HotPriorityQueue() : this(0) { }
	
		public HotPriorityQueue(short preferenceWidth) : this(preferenceWidth, 2048) { }

		public HotPriorityQueue(short preferenceWidth, int initialSize)
		{
			PoolSize = initialSize >> 3 * 7;
			_baseIndex = 0;
			_preferenceWidth = preferenceWidth;
			_queue = new HotPriorityQueueList<short, TValue>(initialSize).PriorityQueue;
#if UseSortedDictionary
      _lists = new SortedDictionary<int, HotPriorityQueueList<int, TValue>>();
#else
			_lists = new SortedList<short, HotPriorityQueueList<short, TValue>>();
#endif
		}


		bool IPriorityQueue<short, TValue>.Any() { return this.Any; }


		public bool Any { get { return _queue.Count > 0 || _lists.Count > 0; } }


		public int Count { get { return _queue.Count; } }


		public int PoolSize { get; set; }


		public void Enqueue(short priority, TValue value)
		{
			Enqueue(new HexKeyValuePair<short, TValue>(priority, value));
		}

		public void Enqueue(HexKeyValuePair<short, TValue> item)
		{
			var index = (short) item.Key >> _preferenceWidth;
			if (index <= _baseIndex)
			{
				_queue.Enqueue(item);
			}
			else if (_lists.Count == 0 && _queue.Count < PoolSize)
			{
				_baseIndex = (short) index;
				_queue.Enqueue(item);
			}
			else
			{
				HotPriorityQueueList<short, TValue> list;
				if (!_lists.TryGetValue((short) index, out list))
				{
					list = new HotPriorityQueueList<short, TValue>();
					_lists.Add((short) index, list);
				}
				list.Add(item);
			}
		}

		public bool TryDequeue(out HexKeyValuePair<short, TValue> result)
		{
			if (_queue.TryDequeue(out result)) return true;
			else if (_lists.Count > 0) return (_queue = GetNextQueue()).TryDequeue(out result);
			else return false;
		}

		public bool TryPeek(out HexKeyValuePair<short, TValue> result)
		{
			if (_queue.TryPeek(out result)) return true;
			else if (_lists.Count > 0) return (_queue = GetNextQueue()).TryPeek(out result);
			else return false;
		}

		private IPriorityQueue<short, TValue> GetNextQueue()
		{
			var list = _lists.First();
			_lists.Remove(list.Key);
			_baseIndex = list.Key;

			return list.Value.PriorityQueue;
		}
	}
}