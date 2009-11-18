﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Loyc.Utilities
{
	/// <summary>A compact auto-enlarging array structure that is intended to be 
	/// used within other data structures. It should only be used internally in
	/// low-level code.
	/// </summary>
	/// <remarks>
	/// InternalList is a struct, not a class, in order to save memory; and for 
	/// maximum performance, it asserts rather than throwing an exception 
	/// when an incorrect array index is used. Besides that, it has an 
	/// InternalArray property that provides access to the internal array. 
	/// For all these reasons one should avoid exposing it in a public API, and 
	/// it should only be used when performance trumps all other concerns.
	/// <para/>
	/// Passing this structure by value is dangerous because changes to a copy 
	/// of the structure may or may not be reflected in the original list. It's
	/// best not to pass it around at all, but if you must pass it, pass it by
	/// reference.
	/// <para/>
	/// Also, do not use the default contructor. Always specify an initial 
	/// capacity (even if it's zero) so that the _array member gets a value. 
	/// This is required because The Add(), Insert() and Resize() functions 
	/// assume _array is not null. If you have to use the default constructor 
	/// for some reason, you can construct the structure properly later by 
	/// calling Clear().
	/// <para/>
	/// InternalList has one nice thing that List(of T) lacks: a Resize() method
	/// and an equivalent Count setter. Which dork at Microsoft decided no one
	/// should be allowed to set the list length directly?
	/// </remarks>
	public struct InternalList<T> : IList<T>
	{
		public static readonly T[] EmptyArray = new T[0];
		private T[] _array;
		private int _count;
		private const int BaseCapacity = 4;

		public InternalList(int capacity)
		{
			_array = capacity != 0 ? new T[capacity] : EmptyArray;
			_count = 0;
		}
		private InternalList(T[] array, int count)
		{
			_array = array; _count = count;
		}

		public int Count
		{
			get { return _count; }
			set { Resize(value); }
		}

		public bool IsEmpty
		{
			get { return _count == 0; }
		}

		/// <summary>Gets or sets the array length.</summary>
		/// <remarks>Changing this property requires O(Count) time and temporary 
		/// space. Attempting to set the capacity lower than Count has no effect.
		/// </remarks>
		public int Capacity
		{
			get { return _array.Length; }
			set {
				if (_array.Length != value && value >= _count)
					_array = CopyToNewArray(_array, _count, value);
			}
		}

		public static T[] CopyToNewArray(T[] _array, int _count, int newCapacity)
		{
			Debug.Assert(_count <= _array.Length);
			Debug.Assert(_count <= newCapacity);
			var a = new T[newCapacity];
			
			if (_count <= 4) {	
				// Unroll loop for small list
				if (_count == 4) {
					// Most common case, assuming BaseCapacity==4
					a[3] = _array[3];
					a[2] = _array[2];
					a[1] = _array[1];
					a[0] = _array[0];
				} else if (_count >= 1) {
					a[0] = _array[0];
					if (_count >= 2) {
						a[1] = _array[1];
						if (_count >= 3)
							a[2] = _array[2];
					}
				}
			} else {
				Array.Copy(_array, a, _count);
			}
			return a;
		}

		private void IncreaseCapacity()
		{
			Capacity = _array.Length + (_array.Length >> 1) + BaseCapacity;
		}

		public void Resize(int newSize)
		{
			if (newSize > _count)
			{
				if (newSize > _array.Length)
				{
					if (newSize <= _array.Length + (_array.Length >> 2)) {
						IncreaseCapacity();
						Debug.Assert(Capacity > newSize);
					} else
						Capacity = newSize;
				}
				_count = newSize;
			}
			else if (newSize < _count)
			{
				if (newSize == 0)
					Clear();
				else if (newSize < (_array.Length >> 2)) {
					_count = newSize;
					Capacity = newSize;
				} else {
					for (int i = newSize; i < _count; i++)
						_array[i] = default(T);
					_count = newSize;
				}
			}
			
		}
		
		public void Insert(int index, T item)
		{
			Debug.Assert((uint)index <= (uint)_array.Length);
			if (_count == _array.Length)
				IncreaseCapacity();
			for (int i = _count; i > index; i--)
				_array[i] = _array[i - 1];
			_array[index] = item;
		}

		public void Add(T item)
		{
			if (_count == _array.Length)
				IncreaseCapacity();
			_array[_count++] = item;
		}

		public void Clear()
		{
			_count = 0;
			_array = EmptyArray;
		}

		public void RemoveAt(int index)
		{
			Debug.Assert((uint)index < (uint)_array.Length);
			_count--;
			for (int i = index; i < _count; i++)
				_array[i] = _array[i + 1];
			_array[_count] = default(T);
		}

		public void RemoveLast()
		{
			Debug.Assert(_count > 0);
			_array[--_count] = default(T);
		}

        public T this[int index]
		{
			get { 
				Debug.Assert((uint)index < (uint)_array.Length);
				return _array[index];
			}
			set {
				Debug.Assert((uint)index < (uint)_array.Length);
				_array[index] = value;
			}
		}

		/// <summary>Makes a copy of the list with the same capacity</summary>
		public InternalList<T> Clone()
		{
			return new InternalList<T>(CopyToNewArray(_array, _count, _array.Length), _count);
		}
		/// <summary>Makes a copy of the list with Capacity = Count</summary>
		public InternalList<T> CloneAndTrim()
		{
			return new InternalList<T>(CopyToNewArray(_array, _count, _count), _count);
		}
		/// <summary>Makes a copy of the list, as an array</summary>
		public T[] ToArray()
		{
			return CopyToNewArray(_array, _count, _count);
		}
		
		#region Boilerplate

		public int IndexOf(T item)
		{
			EqualityComparer<T> comparer = EqualityComparer<T>.Default;
			for (int i = 0; i < Count; i++)
				if (comparer.Equals(this[i], item))
					return i;
			return -1;
		}
		public bool Contains(T item)
		{
			return IndexOf(item) != -1;
		}
		public void CopyTo(T[] array, int arrayIndex)
		{
			foreach (T item in this)
				array[arrayIndex++] = item;
		}
		public bool IsReadOnly
		{
			get { return false; }
		}
		public bool Remove(T item)
		{
			int i = IndexOf(item);
			if (i == -1)
				return false;
			RemoveAt(i);
			return true;
		}
		System.Collections.IEnumerator
				System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		public IEnumerator<T> GetEnumerator()
        {
			for (int i = 0; i < Count; i++)
				yield return this[i];
		}
		public T[] InternalArray
		{
			get { return _array; }
		}

		#endregion
	}
}