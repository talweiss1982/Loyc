﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc.Collections.Impl;
using System.Diagnostics;

namespace Loyc.Collections
{
	/// <summary>
	/// A dictionary class built on top of <c>InternalSet&lt;KeyValuePair&lt;K,V>></c>.
	/// </summary>
	/// <typeparam name="K"></typeparam>
	/// <typeparam name="V"></typeparam>
	/// <remarks>
	/// Benchmarks show that this class is not as fast as the standard <see 
	/// cref="Dictionary{K,V}"/> in most cases. however, it does have some 
	/// advantages:
	/// <ul>
	/// <li>MMap allows null as a key (assuming it is based on the second version
	/// of <see cref="InternalSet{T}"/>).</li>
	/// <li><see cref="TryGetValue"/> and <see cref="ContainsKey"/> do not throw
	/// an incredibly annoying exception if you have the audacity to ask whether 
	/// there is a null key in the collection.</li>
	/// <li>This class supports fast cloning in O(1) time.</li>
	/// <li>You can convert a mutable <see cref="MMap<K,V>"/> into an immutable
	/// <see cref="Map<K,V>"/>, a read-only dictionary that does not change when 
	/// you change the original MMap.</li>
	/// <li>This class has an <see cref="AddRange"/> method.</li>
	/// <li>This class has some bonus features: <see cref="TryGetValue(K, V)"/>
	/// returns a default value if the key is not present; <see cref="AddIfNotPresent"/>
	/// only adds a pair if the collection does not already contain the key;
	/// <see cref="AddOrFind"/> can retrieve the current value and change it to
	/// a new value at the same time; and <see cref="GetAndRemove"/> can get a
	/// value while it is being deleted.</li>
	/// <li>The persistent map operations <see cref="Union"/>, 
	/// <see cref="Intersect"/>, <see cref="Except"/> and <see cref="Xor"/> 
	/// combine two dictionaries to create a new dictionary, without modifying 
	/// either of the original dictionaries. Equally interesting, the methods
	/// <see cref="With"/> and <see cref="Without"/> create a new dictionary
	/// with a single item added or removed.</li>
	/// </ul>
	/// The documentation of <see cref="InternalSet{T}"/> describes how the data 
	/// structure works.
	/// </remarks>
	[Serializable]
	[DebuggerTypeProxy(typeof(DictionaryDebugView<,>))]
	[DebuggerDisplay("Count = {Count}")]
	public class MMap<K, V> : IDictionary<K, V>, ICollection<KeyValuePair<K, V>>, ICloneable<MMap<K, V>>, IAddRange<KeyValuePair<K, V>>, IEqualityComparer<KeyValuePair<K, V>>
	{
		internal InternalSet<KeyValuePair<K, V>> _set;
		private IEqualityComparer<K> _keyComparer;
		internal IEqualityComparer<KeyValuePair<K, V>> Comparer { get { return this; } }
		private int _count;

		public MMap() : this(InternalSet<K>.DefaultComparer) { }
		public MMap(IEqualityComparer<K> comparer) { _keyComparer = comparer; }
		public MMap(IEnumerable<KeyValuePair<K, V>> copy) : this(copy, InternalSet<K>.DefaultComparer) { }
		public MMap(IEnumerable<KeyValuePair<K,V>> copy, IEqualityComparer<K> comparer) { _keyComparer = comparer; AddRange(copy); }
		public MMap(MMap<K, V> clone) : this(clone._set, clone._keyComparer, clone._count) { }
		internal MMap(InternalSet<KeyValuePair<K, V>> set, IEqualityComparer<K> keyComparer, int count)
		{
			_set = set;
			_keyComparer = keyComparer;
			_count = count;
			_set.CloneFreeze();
		}

		public IEqualityComparer<K> KeyComparer { get { return _keyComparer; } }
		public InternalSet<KeyValuePair<K, V>> FrozenInternalSet { get { _set.CloneFreeze(); return _set; } }

		#region Key comparison interface (with explanation)

		/// <summary>Not intended to be called by users.</summary>
		/// <remarks>
		/// The user can provide a <see cref="IEqualityComparer{K}"/> to compare keys. 
		/// However, InternalSet&lt;KeyValuePair&lt;K, V>> requires a comparer that 
		/// can compare <see cref="KeyValuePair<K, V>"/> values. Therefore, MMap 
		/// implements IEqualityComparer&lt;KeyValuePair&lt;K, V>> to provide the 
		/// necessary comparer without an unnecessary memory allocation.
		/// </remarks>
		bool IEqualityComparer<KeyValuePair<K, V>>.Equals(KeyValuePair<K, V> x, KeyValuePair<K, V> y)
		{
 			return _keyComparer.Equals(x.Key, y.Key);
		}
		/// <summary>Not intended to be called by users.</summary>
		int IEqualityComparer<KeyValuePair<K, V>>.GetHashCode(KeyValuePair<K, V> obj)
		{
 			return _keyComparer.GetHashCode(obj.Key);
		}

		#endregion
		
		#region IDictionary<K,V>

		public void Add(K key, V value)
		{
			Add(new KeyValuePair<K,V>(key,value));
		}
		public bool ContainsKey(K key)
		{
			var kvp = new KeyValuePair<K, V>(key, default(V));
			return _set.Find(ref kvp, Comparer);
		}
		public ICollection<K> Keys
		{
			get { return new KeyCollection<K, V>(this); }
		}
		public bool Remove(K key)
		{
			var kvp = new KeyValuePair<K, V>(key, default(V));
			return GetAndRemove(ref kvp);
		}
		public bool TryGetValue(K key, out V value)
		{
			var kvp = new KeyValuePair<K, V>(key, default(V));
			bool result = _set.Find(ref kvp, Comparer);
			value = kvp.Value;
			return result;
		}
		public ICollection<V> Values
		{
			get { return new ValueCollection<K, V>(this); }
		}
		public V this[K key]
		{
			get {
				var kvp = new KeyValuePair<K, V>(key, default(V));
				if (_set.Find(ref kvp, Comparer))
					return kvp.Value;
				throw new KeyNotFoundException();
			}
			set {
				var kvp = new KeyValuePair<K, V>(key, value);
				if (_set.Add(ref kvp, Comparer, true))
					_count++;
			}
		}

		#endregion

		#region ICollection<KeyValuePair<K,V>>

		public void Add(KeyValuePair<K, V> item)
		{
			if (_set.Add(ref item, Comparer, false)) {
				_count++;
				return;
			}
			throw new ArgumentException("The specified key already exists in the map.");
		}
		public void Clear()
		{
			_set.Clear();
			_count = 0;
		}
		public bool Contains(KeyValuePair<K, V> item)
		{
			V value;
			if (!TryGetValue(item.Key, out value))
				return false;
			return object.Equals(value, item.Value);
		}
		public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
		{
			if (_count > array.Length - arrayIndex)
				throw new ArgumentException(Localize.From("CopyTo: Insufficient space in supplied array"));
			_set.CopyTo(array, arrayIndex);
		}
		public int Count
		{
			get { return _count; }
		}
		public bool IsReadOnly
		{
			get { return false; }
		}

		/// <summary>Removes a pair from the map.</summary>
		/// <remarks>The removal occurs only if the value provided matches the 
		/// value that is already associated with the key (value comparison is 
		/// performed using object.Equals()).</remarks>
		/// <returns>True if the pair was removed, false if not.</returns>
		public bool Remove(KeyValuePair<K, V> item)
		{
			V value;
			if (TryGetValue(item.Key, out value))
				if (object.Equals(item.Value, value))
					return Remove(item.Key);
			return false;
		}

		public InternalSet<KeyValuePair<K, V>>.Enumerator GetEnumerator()
		{
			return _set.GetEnumerator();
		}
		IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() { return GetEnumerator(); }
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

		#endregion

		#region Additional functionality: Clone, AddRange, AddIfNotPresent, AddOrFind, GetAndRemove, alt. TryGetValue

		/// <summary>Creates a copy of this map in O(1) time, by marking the current
		/// root node as frozen.</summary>
		public virtual MMap<K, V> Clone()
		{
			return new MMap<K, V>(_set.CloneFreeze(), _keyComparer, _count);
		}

		/// <summary>Merges the contents of the specified map into this map.</summary>
		/// <param name="replaceIfPresent">If true, values in the other collection
		/// replace values in this one. If false, the existing pairs in this map
		/// are not overwritten.</param>
		/// <returns>The number of items that were added.</returns>
		public int AddRange(MMap<K, V> data, bool replaceIfPresent = true)
		{
			int added = _set.UnionWith(data._set, Comparer, replaceIfPresent);
			_count += added;
			return added;
		}
		void IAddRange<KeyValuePair<K, V>>.AddRange(IEnumerable<KeyValuePair<K, V>> data) { AddRange(data, true); }
		void IAddRange<KeyValuePair<K, V>>.AddRange(IListSource<KeyValuePair<K, V>> data) { AddRange(data, true); }
		
		/// <summary>Merges the contents of the specified sequence into this map.</summary>
		/// <param name="replaceIfPresent">If true, values in the other collection
		/// replace values in this one. If false, the existing pairs in this map
		/// are not overwritten.</param>
		/// <returns>The number of items that were added.</returns>
		/// <remarks>Duplicates are allowed in the source data. If 
		/// <c>replaceIfPresent</c> is true, later values take priority over 
		/// earlier values, otherwise earlier values take priority.</remarks>
		public int AddRange(IEnumerable<KeyValuePair<K, V>> data, bool replaceIfPresent = true)
		{
			int added = _set.UnionWith(data, Comparer, replaceIfPresent);
			_count += added;
			return added;
		}

		/// <summary>Adds an item to the map if the key is not present. If the 
		/// key is already present, this method has no effect.</summary>
		/// <returns>True if the pair was added, false if not.</returns>
		public bool AddIfNotPresent(K key, V value)
		{
			var kvp = new KeyValuePair<K, V>(key, value);
			return AddOrFind(ref kvp, false);
		}

		/// <summary>Adds an item to the map if it is not present, retrieves 
		/// the existing key-value pair if the key is present, and optionally
		/// replaces the existing pair with a new pair.</summary>
		/// <param name="pair">When calling this method, pair.Key specifies the
		/// key that you want to search for in the map. If the key is not found
		/// then the pair is added to the map; if the key is found, the pair is
		/// replaced with the existing pair that was found in the map.</param>
		/// <param name="replaceIfPresent">This parameter specifies what to do
		/// if the key is found in the map. If this parameter is true, the 
		/// existing pair is replaced with the specified new pair (in fact the
		/// pair in the map is swapped with the <c>pair</c> parameter). If this
		/// parameter is false, the existing pair is left unmodified and a copy
		/// of it is stored in the <c>pair</c> parameter.</param>
		/// <returns>True if the pair's key did NOT exist and was added, false 
		/// if the key already existed.</returns>
		public bool AddOrFind(ref KeyValuePair<K, V> pair, bool replaceIfPresent)
		{
			if (_set.Add(ref pair, Comparer, replaceIfPresent)) {
				_count++;
				return true;
			}
			return false;
		}

		/// <summary>Gets the value associated with the specified key, then
		/// removes the pair with that key from the dictionary.</summary>
		/// <param name="key">Key to search for.</param>
		/// <param name="valueRemoved">The value that was removed. If the key 
		/// is not found, the value of this parameter is left unchanged.</param>
		/// <returns>True if a pair was removed, false if not.</returns>
		public bool GetAndRemove(K key, ref V valueRemoved)
		{
			var kvp = new KeyValuePair<K, V>(key, default(V));
			if (_set.Remove(ref kvp, Comparer)) {
				_count--;
				valueRemoved = kvp.Value;
				return true;
			}
			return false;
		}

		/// <summary>Gets the pair associated with <c>pair.Key</c>, then
		/// removes the pair with that key from the dictionary.</summary>
		/// <param name="pair">Specifies the key to search for. On return, if the
		/// key was found, this holds both the key and value that used to be in
		/// the dictionary.</param>
		/// <returns>True if a pair was removed, false if not.</returns>
		public bool GetAndRemove(ref KeyValuePair<K, V> pair)
		{
			if (_set.Remove(ref pair, Comparer)) {
				_count--;
				return true;
			}
			return false;
		}

		/// <summary>Retrieves the value associated with the specified key,
		/// or returns <c>defaultValue</c> if the key is not found.</summary>
		public V TryGetValue(K key, V defaultValue)
		{
			var kvp = new KeyValuePair<K, V>(key, defaultValue);
			_set.Find(ref kvp, Comparer);
			return kvp.Value;
		}
		
		#endregion

		#region Persistent map operations: With, Without, Union, Except, Intersect, Xor

		/// <summary>Returns a copy of the current map with an additional key-value pair.</summary>
		/// <paparam name="replaceIfPresent">If true, the existing key-value pair is replaced if present. 
		/// Otherwise, the existing key-value pair is left unchanged.</paparam>
		/// <returns>A map with the specified key. If the key was already present 
		/// and replaceIfPresent is false, the same set ('this') is returned.</remarks>
		public MMap<K, V> With(K key, V value, bool replaceIfPresent = true)
		{
			var set = _set.CloneFreeze();
			var item = new KeyValuePair<K, V>(key, value);
			if (set.Add(ref item, Comparer, replaceIfPresent))
				return new MMap<K, V>(set, _keyComparer, _count + 1);
			if (replaceIfPresent)
				return new MMap<K, V>(set, _keyComparer, _count);
			return this;
		}
		/// <summary>Returns a copy of the current map without the specified key.</summary>
		/// <returns>A map without the specified key. If the key was not present,
		/// the same set ('this') is returned.</remarks>
		public MMap<K, V> Without(K key)
		{
			var set = _set.CloneFreeze();
			var item = new KeyValuePair<K, V>(key, default(V));
			if (set.Remove(ref item, Comparer))
				return new MMap<K, V>(set, _keyComparer, _count - 1);
			return this;
		}
		public MMap<K,V> Union(Map<K,V> other, bool replaceWithValuesFromOther = false) { return Union(other._set, replaceWithValuesFromOther); }
		public MMap<K,V> Union(MMap<K,V> other, bool replaceWithValuesFromOther = false) { return Union(other._set, replaceWithValuesFromOther); }
		internal MMap<K,V> Union(InternalSet<KeyValuePair<K,V>> other, bool replaceWithValuesFromOther = false)
		{
			var set = _set.CloneFreeze();
			int count2 = _count + set.UnionWith(other, Comparer, replaceWithValuesFromOther);
			return new MMap<K,V>(set, _keyComparer, count2);
		}
		public MMap<K,V> Intersect(Map<K,V> other) { return Intersect(other._set, other.Comparer); }
		public MMap<K,V> Intersect(MMap<K,V> other) { return Intersect(other._set, other.Comparer); }
		internal MMap<K,V> Intersect(InternalSet<KeyValuePair<K,V>> other, IEqualityComparer<KeyValuePair<K,V>> otherComparer)
		{
			var set = _set.CloneFreeze();
			int count2 = _count - set.IntersectWith(other, otherComparer);
			return new MMap<K, V>(set, _keyComparer, count2);
		}
		public MMap<K,V> Except(Map<K,V> other) { return Except(other._set); }
		public MMap<K,V> Except(MMap<K,V> other) { return Except(other._set); }
		internal MMap<K,V> Except(InternalSet<KeyValuePair<K,V>> other)
		{
			var set = _set.CloneFreeze();
			int count2 = _count - set.ExceptWith(other, Comparer);
			return new MMap<K, V>(set, _keyComparer, count2);
		}
		public MMap<K,V> Xor(Map<K,V> other) { return Xor(other._set); }
		public MMap<K,V> Xor(MMap<K,V> other) { return Xor(other._set); }
		internal MMap<K,V> Xor(InternalSet<KeyValuePair<K,V>> other)
		{
			var set = _set.CloneFreeze();
			int count2 = _count + set.SymmetricExceptWith(other, Comparer);
			return new MMap<K, V>(set, _keyComparer, count2);
		}

		#endregion

		public static explicit operator Map<K, V>(MMap<K, V> copy) 
		{
			var map = new Map<K, V>(copy._set, copy._keyComparer, copy._count);
			Debug.Assert(copy._set.IsRootFrozen);
			return map;
		}
	}
}
