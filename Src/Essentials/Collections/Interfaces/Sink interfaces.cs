﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Loyc.Collections
{
	/// <summary>Interface for depositing items. Has the Add(T) method.</summary>
	public interface IHasAdd<in T>
	{
		void Add(T item);
	}

	/// <summary>Represents a write-only collection: you can modify it, but you
	/// cannot learn what it contains.</summary>
	#if CSharp4
	public interface ISinkCollection<in T> : IHasAdd<T>
	#else
	public interface ISinkCollection<T>
	#endif
	{
		//inherited void Add(T item);
		void Clear();
		bool Remove(T item);
	}

	/// <summary>Represents a write-only array.</summary>
	#if CSharp4
	public interface ISinkArray<in T>
	#else
	public interface ISinkArray<T> : ICount
	#endif
	{
		T this[int index] { set; }
	}

	/// <summary>Represents a write-only indexable list class.</summary>
	#if CSharp4
	public interface ISinkList<in T> : ISinkCollection<T>, ISinkArray<T>
	#else
	public interface ISinkList<T> : ISinkCollection<T>, ISinkArray<T>
	#endif
	{
	}
}
