using System;
using System.Collections;

namespace TinyOSCore;

/// <summary>
///     A collection that stores <see cref='TinyOSCore.Process' /> objects.
/// </summary>
/// <seealso cref='TinyOSCore.ProcessCollection' />
[Serializable]
public class ProcessCollection : CollectionBase, IComparer
{
    /// <summary>
    ///     Initializes a new instance of <see cref='TinyOSCore.ProcessCollection' />.
    /// </summary>
    public ProcessCollection()
    {
    }

    /// <summary>
    ///     Initializes a new instance of <see cref='TinyOSCore.ProcessCollection' /> based on another
    ///     <see cref='TinyOSCore.ProcessCollection' />.
    /// </summary>
    /// <param name='value'>
    ///     A <see cref='TinyOSCore.ProcessCollection' /> from which the contents are copied
    /// </param>
    public ProcessCollection(ProcessCollection value)
    {
        AddRange(value);
    }

    /// <summary>
    ///     Initializes a new instance of <see cref='TinyOSCore.ProcessCollection' /> containing any array of
    ///     <see cref='TinyOSCore.Process' /> objects.
    /// </summary>
    /// <param name='value'>
    ///     A array of <see cref='TinyOSCore.Process' /> objects with which to intialize the collection
    /// </param>
    public ProcessCollection(Process[] value)
    {
        AddRange(value);
    }

    /// <summary>
    ///     Represents the entry at the specified index of the <see cref='TinyOSCore.Process' />.
    /// </summary>
    /// <param name='index'>The zero-based index of the entry to locate in the collection.</param>
    /// <value>
    ///     The entry at the specified index of the collection.
    /// </value>
    /// <exception cref='System.ArgumentOutOfRangeException'>
    ///     <paramref name='index' /> is outside the valid range of indexes
    ///     for the collection.
    /// </exception>
    public Process this[int index]
    {
        get => (Process)List[index];
        set => List[index] = value;
    }


    /// <summary>
    ///     Implemented for IComparable.
    /// </summary>
    /// <param name="x">A Process object</param>
    /// <param name="y">A Process object</param>
    /// <returns>a comparison int from CompareTo</returns>
    public int Compare(object x, object y)
    {
        var px = (Process)x;
        var py = (Process)y;

        if (px != null)
        {
            return px.CompareTo(py);
        }

        return 0;
    }

    /// <summary>
    ///     Adds a <see cref='TinyOSCore.Process' /> with the specified value to the
    ///     <see cref='TinyOSCore.ProcessCollection' /> .
    /// </summary>
    /// <param name='value'>The <see cref='TinyOSCore.Process' /> to add.</param>
    /// <returns>
    ///     The index at which the new element was inserted.
    /// </returns>
    /// <seealso cref='TinyOSCore.ProcessCollection.AddRange(ProcessCollection)' />
    public int Add(Process value)
    {
        return List.Add(value);
    }

    /// <summary>
    ///     Copies the elements of an array to the end of the <see cref='TinyOSCore.ProcessCollection' />.
    /// </summary>
    /// <param name='value'>
    ///     An array of type <see cref='TinyOSCore.Process' /> containing the objects to add to the collection.
    /// </param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <seealso cref='TinyOSCore.ProcessCollection.Add' />
    public void AddRange(Process[] value)
    {
        for (var i = 0; i < value.Length; i = i + 1)
        {
            Add(value[i]);
        }
    }

    /// <summary>
    ///     Adds the contents of another <see cref='TinyOSCore.ProcessCollection' /> to the end of the collection.
    /// </summary>
    /// <param name='value'>
    ///     A <see cref='TinyOSCore.ProcessCollection' /> containing the objects to add to the collection.
    /// </param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <seealso cref='TinyOSCore.ProcessCollection.Add' />
    public void AddRange(ProcessCollection value)
    {
        for (var i = 0; i < value.Count; i = i + 1)
        {
            Add(value[i]);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the
    ///     <see cref='TinyOSCore.ProcessCollection' /> contains the specified <see cref='TinyOSCore.Process' />.
    /// </summary>
    /// <param name='value'>The <see cref='TinyOSCore.Process' /> to locate.</param>
    /// <returns>
    ///     <see langword='true' /> if the <see cref='TinyOSCore.Process' /> is contained in the collection;
    ///     otherwise, <see langword='false' />.
    /// </returns>
    /// <seealso cref='TinyOSCore.ProcessCollection.IndexOf' />
    public bool Contains(Process value)
    {
        return List.Contains(value);
    }

    /// <summary>
    ///     Sorts the list of OS.RunningProcesses based on <see cref="Process.ProcessControlBlock.Priority" />
    /// </summary>
    public void Sort()
    {
        InnerList.Sort(this);
    }

    /// <summary>
    ///     Copies the <see cref='TinyOSCore.ProcessCollection' /> values to a one-dimensional <see cref='System.Array' />
    ///     instance at the
    ///     specified index.
    /// </summary>
    /// <param name='array'>
    ///     The one-dimensional <see cref='System.Array' /> that is the destination of the values copied from
    ///     <see cref='TinyOSCore.ProcessCollection' /> .
    /// </param>
    /// <param name='index'>The index in <paramref name='array' /> where copying begins.</param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <exception cref='System.ArgumentException'>
    ///     <paramref name='array' /> is multidimensional. -or- The number of elements
    ///     in the <see cref='TinyOSCore.ProcessCollection' /> is greater than the available space between
    ///     <paramref name='index' /> and the end of <paramref name='array' />.
    /// </exception>
    /// <exception cref='System.ArgumentNullException'><paramref name='array' /> is <see langword='null' />. </exception>
    /// <exception cref='System.ArgumentOutOfRangeException'>
    ///     <paramref name='index' /> is less than <paramref name='array' />'s
    ///     lowbound.
    /// </exception>
    /// <seealso cref='System.Array' />
    public void CopyTo(Process[] array, int index)
    {
        List.CopyTo(array, index);
    }

    /// <summary>
    ///     Returns the index of a <see cref='TinyOSCore.Process' /> in
    ///     the <see cref='TinyOSCore.ProcessCollection' /> .
    /// </summary>
    /// <param name='value'>The <see cref='TinyOSCore.Process' /> to locate.</param>
    /// <returns>
    ///     The index of the <see cref='TinyOSCore.Process' /> of <paramref name='value' /> in the
    ///     <see cref='TinyOSCore.ProcessCollection' />, if found; otherwise, -1.
    /// </returns>
    /// <seealso cref='TinyOSCore.ProcessCollection.Contains' />
    public int IndexOf(Process value)
    {
        return List.IndexOf(value);
    }

    /// <summary>
    ///     Inserts a <see cref='TinyOSCore.Process' /> into the <see cref='TinyOSCore.ProcessCollection' /> at the specified
    ///     index.
    /// </summary>
    /// <param name='index'>The zero-based index where <paramref name='value' /> should be inserted.</param>
    /// <param name="value">The <see cref='TinyOSCore.Process' /> to insert.</param>
    /// <returns>None.</returns>
    /// <seealso cref='TinyOSCore.ProcessCollection.Add' />
    public void Insert(int index, Process value)
    {
        List.Insert(index, value);
    }

    /// <summary>
    ///     Returns an enumerator that can iterate through
    ///     the <see cref='TinyOSCore.ProcessCollection' /> .
    /// </summary>
    /// <returns>None.</returns>
    /// <seealso cref='System.Collections.IEnumerator' />
    public new ProcessEnumerator GetEnumerator()
    {
        return new ProcessEnumerator(this);
    }

    /// <summary>
    ///     Removes a specific <see cref='TinyOSCore.Process' /> from the
    ///     <see cref='TinyOSCore.ProcessCollection' /> .
    /// </summary>
    /// <param name='value'>
    ///     The <see cref='TinyOSCore.Process' /> to remove from the
    ///     <see cref='TinyOSCore.ProcessCollection' /> .
    /// </param>
    /// <returns>None.</returns>
    /// <exception cref='System.ArgumentException'><paramref name='value' /> is not found in the Collection. </exception>
    public void Remove(Process value)
    {
        List.Remove(value);
    }

    /// <summary>
    ///     Provided for "foreach" support with this collection
    /// </summary>
    public class ProcessEnumerator : object, IEnumerator
    {
        private readonly IEnumerator _baseEnumerator;

        /// <summary>
        ///     Public constructor for an ProcessEnumerator
        /// </summary>
        /// <param name="mappings">The <see cref="ProcessCollection" />we are going to iterate over</param>
        public ProcessEnumerator(ProcessCollection mappings)
        {
            _baseEnumerator = mappings.GetEnumerator();
        }

        /// <summary>
        ///     The current <see cref="Process" />
        /// </summary>
        public Process Current => (Process)_baseEnumerator.Current;

        /// <summary>
        ///     The current IEnumerator interface
        /// </summary>
        object IEnumerator.Current => _baseEnumerator.Current;

        /// <summary>
        ///     Move to the next Process
        /// </summary>
        /// <returns>true or false based on success</returns>
        bool IEnumerator.MoveNext()
        {
            return _baseEnumerator.MoveNext();
        }

        /// <summary>
        ///     Reset the cursor
        /// </summary>
        void IEnumerator.Reset()
        {
            _baseEnumerator.Reset();
        }

        /// <summary>
        ///     Move to the next Process
        /// </summary>
        /// <returns>true or false based on success</returns>
        public bool MoveNext()
        {
            return _baseEnumerator.MoveNext();
        }

        /// <summary>
        ///     Reset the cursor
        /// </summary>
        public void Reset()
        {
            _baseEnumerator.Reset();
        }
    }
}