﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Loyc.Collections
{
	/// <summary>A reversed of an <see cref="IList{T}"/>. TODO: unit tests.</summary>
	[Serializable]
	[DebuggerTypeProxy(typeof(CollectionDebugView<>)), DebuggerDisplay("Count = {Count}")]
	public struct ReversedList<T> : IListEx<T>, IListRangeMethods<T>
	{
		IList<T> _list;
		public ReversedList(IList<T> list) { _list = list; }

		public IList<T> OriginalList { get { return _list; } }

		/// <summary>Returns true iff the parameter 'obj' is a wrapper around the same object that this object wraps.</summary>
		public override bool Equals(object obj)
		{
			return obj is ReversedList<T> && _list.Equals((ReversedList<T>)obj);
		}
		/// <summary>Returns the hashcode of the wrapped object.</summary>
		public override int GetHashCode()
		{
			return _list.GetHashCode();
		}
		/// <summary>Returns ToString() of the wrapped object.</summary>
		public override string ToString()
		{
			return _list.ToString();
		}

		public int Count
		{
			get { return _list.Count; }
		}
		public T this[int index]
		{
			get { return _list[_list.Count - 1 - index]; }
			set { _list[_list.Count - 1 - index] = value; }
		}
		public int IndexOf(T item)
		{
			var comp = EqualityComparer<T>.Default;
			for (int i = _list.Count - 1; i >= 0; i--)
				if (comp.Equals(item, _list[i]))
					return _list.Count - 1 - i;
			return -1;
		}

		public void Insert(int index, T item)
		{
			_list.Insert(_list.Count - index, item);
		}

		public void RemoveAt(int index)
		{
			_list.RemoveAt(_list.Count - 1 - index);
		}

		public void Add(T item)
		{
			_list.Insert(0, item);
		}

		public void Clear()
		{
			_list.Clear();
		}

		public bool Contains(T item)
		{
			return _list.Contains(item);
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			ListExt.CopyTo(this, array, arrayIndex);
		}

		public bool IsReadOnly
		{
			get { return _list.IsReadOnly; }
		}

		public bool Remove(T item)
		{
			int i = IndexOf(item);
			if (i > -1) {
				RemoveAt(i);
				return true;
			}
			return false;
		}

		public IEnumerator<T> GetEnumerator()
		{
			for (int i = _list.Count - 1; i >= 0; i--)
				yield return _list[i];
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public int RemoveAll(Predicate<T> match)
		{
			return ListExt.RemoveAll(this, match);
		}

		public void AddRange(IEnumerable<T> list)
		{
			InsertRange(Count, list);
		}

		public void AddRange(IListSource<T> list)
		{
			InsertRange(Count, list);
		}

		public bool TrySet(int index, T value)
		{
			int c = Count;
			if ((uint)index >= (uint)c)
				return false;
			_list[c - 1 - index] = value;
			return true;
		}

		public T TryGet(int index, ref bool fail)
		{
			int c = Count;
			if ((uint)index < (uint)c)
				return _list[c - 1 - index];
			fail = true;
			return default(T);
		}

		public IRange<T> Slice(int start, int count = int.MaxValue)
		{
			return new Slice_<T>(this, start, count);
		}

		public void InsertRange(int index, IEnumerable<T> list)
		{
			int spaceNeeded = list.Count();
			int index2 = _list.Count - index;
			ListExt.InsertRangeHelper(_list, index2, spaceNeeded);
			index2 += spaceNeeded;
			var e = list.GetEnumerator();
			while (e.MoveNext())
				_list[--index2] = e.Current;
		}

		public void InsertRange(int index, IListSource<T> list)
		{
			int spaceNeeded = list.Count;
			int index2 = _list.Count - index;
			ListExt.InsertRangeHelper(_list, index2, spaceNeeded);
			index2 += spaceNeeded - 1;
			for (int i = 0; i < spaceNeeded; i++)
				_list[index2 - i] = list[i];
		}

		public void RemoveRange(int index, int amount)
		{
			_list.RemoveRange(Count - index - amount, amount);
		}

		public void Sort(int index, int count, Comparison<T> comp)
		{
			ListExt.Sort(this, index, count, comp);
		}
	}
}
