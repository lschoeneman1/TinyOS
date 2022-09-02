using System;
using System.Collections;

namespace TinyOSCore;

/// <summary>
///     A collection that stores <see cref='InstructionCollection' /> objects.
/// </summary>
/// <seealso cref='TinyOSCore' />
[Serializable]
public class InstructionCollection : CollectionBase
{
    /// <summary>
    ///     Initializes a new instance of <see cref='InstructionCollection' />.
    /// </summary>
    public InstructionCollection()
    {
    }

    /// <summary>
    ///     Initializes a new instance of <see cref='InstructionCollection' /> based on another
    ///     <see cref='InstructionCollection' />.
    /// </summary>
    /// <param name='value'>
    ///     A <see cref='InstructionCollection' /> from which the contents are copied
    /// </param>
    public InstructionCollection(InstructionCollection value)
    {
        AddRange(value);
    }

    /// <summary>
    ///     Initializes a new instance of <see cref='InstructionCollection' /> containing any array of
    ///     <see cref='Instruction' /> objects.
    /// </summary>
    /// <param name='value'>
    ///     A array of <see cref='Instruction' /> objects with which to intialize the collection
    /// </param>
    public InstructionCollection(Instruction[] value)
    {
        AddRange(value);
    }

    /// <summary>
    ///     Represents the entry at the specified index of the <see cref='Instruction' />.
    /// </summary>
    /// <param name='index'>The zero-based index of the entry to locate in the collection.</param>
    /// <value>
    ///     The entry at the specified index of the collection.
    /// </value>
    /// <exception cref='System'><paramref name='index' /> is outside the valid range of indexes for the collection.</exception>
    public Instruction this[int index]
    {
        get => (Instruction)List[index];
        set => List[index] = value;
    }

    /// <summary>
    ///     Adds a <see cref='InstructionCollection' /> with the specified value to the
    ///     <see cref='TinyOSCore' /> .
    /// </summary>
    /// <param name='value'>The <see cref='InstructionCollection' /> to add.</param>
    /// <returns>
    ///     The index at which the new element was inserted.
    /// </returns>
    /// <seealso cref='TinyOSCore' />
    public int Add(Instruction value)
    {
        return List.Add(value);
    }

    /// <summary>
    ///     Copies the elements of an array to the end of the <see cref='InstructionCollection' />.
    /// </summary>
    /// <param name='value'>
    ///     An array of type <see cref='Add' /> containing the objects to add to the collection.
    /// </param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <seealso cref='InstructionCollection' />
    public void AddRange(Instruction[] value)
    {
        for (var i = 0; i < value.Length; i = i + 1)
        {
            Add(value[i]);
        }
    }

    /// <summary>
    ///     Adds the contents of another <see cref='InstructionCollection' /> to the end of the collection.
    /// </summary>
    /// <param name='value'>
    ///     A <see cref='InstructionCollection' /> containing the objects to add to the collection.
    /// </param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <seealso cref='Add' />
    public void AddRange(InstructionCollection value)
    {
        for (var i = 0; i < value.Count; i = i + 1)
        {
            Add(value[i]);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the
    ///     <see cref='InstructionCollection' /> contains the specified <see cref='Instruction' />.
    /// </summary>
    /// <param name='value'>The <see cref='InstructionCollection' /> to locate.</param>
    /// <returns>
    ///     true if the Instruction is contained in the collection;
    ///     otherwise false
    /// </returns>
    public bool Contains(Instruction value)
    {
        return List.Contains(value);
    }

    /// <summary>
    ///     Copies the <see cref='InstructionCollection' /> values to a one-dimensional <see cref='System.Array' /> instance at
    ///     the
    ///     specified index.
    /// </summary>
    /// <param name='array'>
    ///     The one-dimensional <see cref='System.Array' /> that is the destination of the values copied from
    ///     <see cref='InstructionCollection' /> .
    /// </param>
    /// <param name='index'>The index in <paramref name='array' /> where copying begins.</param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <exception cref='System.ArgumentException'>
    ///     <paramref name='array' /> is multidimensional. -or- The number of elements
    ///     in the <see cref='InstructionCollection' /> is greater than the available space between <paramref name='index' />
    ///     and the end of <paramref name='array' />.
    /// </exception>
    /// <exception cref='System.ArgumentNullException'><paramref name='array' /> is <see langword='null' />. </exception>
    /// <exception cref='System.ArgumentOutOfRangeException'>
    ///     <paramref name='index' /> is less than <paramref name='array' />'s
    ///     lowbound.
    /// </exception>
    /// <seealso cref='System.Array' />
    public void CopyTo(Instruction[] array, int index)
    {
        List.CopyTo(array, index);
    }

    /// <summary>
    ///     Returns the index of a <see cref='InstructionCollection' /> in
    ///     the <see cref='TinyOSCore' /> .
    /// </summary>
    /// <param name='value'>The <see cref='InstructionCollection' /> to locate.</param>
    /// <returns>
    ///     The index of the instruction <paramref name='value' /> in the
    ///     <see cref='Contains' />, if found; otherwise, -1.
    /// </returns>
    public int IndexOf(Instruction value)
    {
        return List.IndexOf(value);
    }

    /// <summary>
    ///     Inserts a <see cref='InstructionCollection' /> into the <see cref='TinyOSCore' /> at the specified index.
    /// </summary>
    /// <param name='index'>The zero-based index where <paramref name='value' /> should be inserted.</param>
    /// <param name='value'>The <see cref='InstructionCollection' /> to insert.</param>
    /// <returns>None.</returns>
    /// <seealso cref='TinyOSCore' />
    public void Insert(int index, Instruction value)
    {
        List.Insert(index, value);
    }

    /// <summary>
    ///     Returns an enumerator that can iterate through
    ///     the <see cref='InstructionCollection' /> .
    /// </summary>
    /// <returns>None.</returns>
    /// <seealso cref='System.Collections.IEnumerator' />
    public new InstructionEnumerator GetEnumerator()
    {
        return new InstructionEnumerator(this);
    }

    /// <summary>
    ///     Removes a specific <see cref='InstructionCollection' /> from the
    ///     <see cref='TinyOSCore' /> .
    /// </summary>
    /// <param name='value'>The instruction to remove from the collection /> .</param>
    /// <returns>None.</returns>
    /// <exception cref='ArgumentException'><paramref name='value' /> is not found in the Collection. </exception>
    public void Remove(Instruction value)
    {
        List.Remove(value);
    }

    /// <summary>
    ///     Provided for "foreach" support with this collection
    /// </summary>
    public class InstructionEnumerator : object, IEnumerator
    {
        private readonly IEnumerator _baseEnumerator;

        /// <summary>
        ///     Public constructor for an InstructionEnumerator
        /// </summary>
        /// <param name="mappings">The <see cref="InstructionCollection" />we are going to iterate over</param>
        public InstructionEnumerator(InstructionCollection mappings)
        {
            _baseEnumerator = mappings.GetEnumerator();
        }

        /// <summary>
        ///     The current <see cref="Instruction" />
        /// </summary>
        public Instruction Current => (Instruction)_baseEnumerator.Current;

        /// <summary>
        ///     The current IEnumerator interface
        /// </summary>
        object IEnumerator.Current => _baseEnumerator.Current;

        /// <summary>
        ///     Move to the next Instruction
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
        ///     Move to the next Instruction
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