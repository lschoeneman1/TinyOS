using System;
using System.Collections;
using System.Collections.Generic;


namespace TinyOSCore.Cpu;

/// <summary>
///     A collection that stores <see cref='Instruction' /> objects.
/// </summary>
/// <seealso cref='InstructionCollection' />
[Serializable]
public class InstructionCollection : IEnumerable<Instruction>
{

    private List<Instruction> _instructions = new List<Instruction>();

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
    ///     Adds a <see cref='Instruction' /> with the specified value to the
    ///     <see cref='InstructionCollection' /> .
    /// </summary>
    /// <param name='value'>The <see cref='Instruction' /> to add.</param>
    /// <returns>
    ///     The index at which the new element was inserted.
    /// </returns>
    public int Add(Instruction value)
    {
        _instructions.Add(value);
        return _instructions.Count;
    }

    /// <summary>
    ///     Adds the contents of another <see cref='InstructionCollection' /> to the end of the collection.
    /// </summary>
    /// <param name='instructionCollection'>
    ///     A <see cref='InstructionCollection' /> containing the objects to add to the collection.
    /// </param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <seealso cref='Add' />
    public void AddRange(InstructionCollection instructionCollection)
    {
        foreach (var t in instructionCollection)
        {
            Add(t);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the
    ///     <see cref='InstructionCollection' /> contains the specified <see cref='Instruction' />.
    /// </summary>
    /// <param name='instruction'>The <see cref='Instruction' /> to locate.</param>
    /// <returns>
    ///     <see langword='true' /> if the <see cref='Instruction' /> is contained in the collection;
    ///     otherwise, <see langword='false' />.
    /// </returns>
    /// <seealso cref='IndexOf' />
    public bool Contains(Instruction instruction)
    {
        return _instructions.Contains(instruction);
    }

    /// <summary>
    ///     Returns the index of a <see cref='Instruction' /> in
    ///     the <see cref='InstructionCollection' /> .
    /// </summary>
    /// <param name='value'>The <see cref='Instruction' /> to locate.</param>
    /// <returns>
    ///     The index of the <see cref='Instruction' /> of <paramref name='value' /> in the
    ///     <see cref='InstructionCollection' />, if found; otherwise, -1.
    /// </returns>
    /// <seealso cref='Contains' />
    public int IndexOf(Instruction value)
    {
        return _instructions.IndexOf(value);
    }

    ///// <summary>
    /////     Removes a specific <see cref='Instruction' /> from the
    /////     <see cref='InstructionCollection' /> .
    ///// </summary>
    ///// <param name='instruction'>The <see cref='Instruction' /> to remove from the <see cref='InstructionCollection' /> .</param>
    ///// <returns>None.</returns>
    ///// <exception cref='ArgumentException'><paramref name='instruction' /> is not found in the Collection. </exception>
    //public void Remove(Instruction instruction)
    //{
    //    _instructions.Remove(instruction);
    //}

    IEnumerator<Instruction> IEnumerable<Instruction>.GetEnumerator()
    {
        return ((IEnumerable<Instruction>)_instructions).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_instructions).GetEnumerator();
    }
}
/// <summary>
    ///     Provided for "foreach" support with this collection
    /// </summary>
//    public class InstructionEnumerator : object, IEnumerator
//    {
//        private readonly IEnumerator baseEnumerator;

//        private readonly IEnumerable temp;

//        /// <summary>
//        ///     Public constructor for an InstructionEnumerator
//        /// </summary>
//        /// <param name="mappings">The <see cref="InstructionCollection" />we are going to iterate over</param>
//        public InstructionEnumerator(InstructionCollection mappings)
//        {
//            temp = mappings;
//            baseEnumerator = temp.GetEnumerator();
//        }

//        /// <summary>
//        ///     The current <see cref="Instruction" />
//        /// </summary>
//        public Instruction Current => (Instruction)baseEnumerator.Current;

//        /// <summary>
//        ///     The current IEnumerator interface
//        /// </summary>
//        object IEnumerator.Current => baseEnumerator.Current;

//        /// <summary>
//        ///     Move to the next Instruction
//        /// </summary>
//        /// <returns>true or false based on success</returns>
//        bool IEnumerator.MoveNext()
//        {
//            return baseEnumerator.MoveNext();
//        }

//        /// <summary>
//        ///     Reset the cursor
//        /// </summary>
//        void IEnumerator.Reset()
//        {
//            baseEnumerator.Reset();
//        }

//        /// <summary>
//        ///     Move to the next Instruction
//        /// </summary>
//        /// <returns>true or false based on success</returns>
//        public bool MoveNext()
//        {
//            return baseEnumerator.MoveNext();
//        }

//        /// <summary>
//        ///     Reset the cursor
//        /// </summary>
//        public void Reset()
//        {
//            baseEnumerator.Reset();
//        }
//    }
//}


/////// <summary>
///////     A collection that stores <see cref='Instruction' /> objects.
/////// </summary>
/////// <seealso cref='InstructionCollection' />
////[Serializable]
////public class InstructionCollection : CollectionBase
////{
////    /// <summary>
////    ///     Initializes a new instance of <see cref='InstructionCollection' />.
////    /// </summary>
////    public InstructionCollection()
////    {
////    }

////    /// <summary>
////    ///     Initializes a new instance of <see cref='InstructionCollection' /> based on another
////    ///     <see cref='InstructionCollection' />.
////    /// </summary>
////    /// <param name='value'>
////    ///     A <see cref='InstructionCollection' /> from which the contents are copied
////    /// </param>
////    public InstructionCollection(InstructionCollection value)
////    {
////        AddRange(value);
////    }

////    /// <summary>
////    ///     Initializes a new instance of <see cref='InstructionCollection' /> containing any array of
////    ///     <see cref='Instruction' /> objects.
////    /// </summary>
////    /// <param name='value'>
////    ///     A array of <see cref='Instruction' /> objects with which to intialize the collection
////    /// </param>
////    public InstructionCollection(Instruction[] value)
////    {
////        AddRange(value);
////    }

////    /// <summary>
////    ///     Represents the entry at the specified index of the <see cref='Instruction' />.
////    /// </summary>
////    /// <param name='index'>The zero-based index of the entry to locate in the collection.</param>
////    /// <value>
////    ///     The entry at the specified index of the collection.
////    /// </value>
////    /// <exception cref='ArgumentOutOfRangeException'>
////    ///     <paramref name='index' /> is outside the valid range of indexes
////    ///     for the collection.
////    /// </exception>
////    public Instruction this[int index]
////    {
////        get => (Instruction)List[index];
////        set => List[index] = value;
////    }

////    /// <summary>
////    ///     Adds a <see cref='Instruction' /> with the specified value to the
////    ///     <see cref='InstructionCollection' /> .
////    /// </summary>
////    /// <param name='value'>The <see cref='Instruction' /> to add.</param>
////    /// <returns>
////    ///     The index at which the new element was inserted.
////    /// </returns>
////    /// <seealso cref='AddRange(Instruction[])' />
////    public int Add(Instruction value)
////    {
////        return List.Add(value);
////    }

////    /// <summary>
////    ///     Copies the elements of an array to the end of the <see cref='InstructionCollection' />.
////    /// </summary>
////    /// <param name='value'>
////    ///     An array of type <see cref='Instruction' /> containing the objects to add to the collection.
////    /// </param>
////    /// <returns>
////    ///     None.
////    /// </returns>
////    /// <seealso cref='Add' />
////    public void AddRange(Instruction[] value)
////    {
////        foreach (var t in value)
////        {
////            Add(t);
////        }
////    }

////    /// <summary>
////    ///     Adds the contents of another <see cref='InstructionCollection' /> to the end of the collection.
////    /// </summary>
////    /// <param name='value'>
////    ///     A <see cref='InstructionCollection' /> containing the objects to add to the collection.
////    /// </param>
////    /// <returns>
////    ///     None.
////    /// </returns>
////    /// <seealso cref='Add' />
////    public void AddRange(InstructionCollection value)
////    {
////        foreach (var t in value)
////        {
////            Add(t);
////        }
////    }

////    /// <summary>
////    ///     Gets a value indicating whether the
////    ///     <see cref='InstructionCollection' /> contains the specified <see cref='Instruction' />.
////    /// </summary>
////    /// <param name='value'>The <see cref='Instruction' /> to locate.</param>
////    /// <returns>
////    ///     <see langword='true' /> if the <see cref='Instruction' /> is contained in the collection;
////    ///     otherwise, <see langword='false' />.
////    /// </returns>
////    /// <seealso cref='IndexOf' />
////    public bool Contains(Instruction value)
////    {
////        return List.Contains(value);
////    }

////    /// <summary>
////    ///     Copies the <see cref='InstructionCollection' /> values to a one-dimensional <see cref='Array' /> instance at
////    ///     the
////    ///     specified index.
////    /// </summary>
////    /// <param name='array'>
////    ///     The one-dimensional <see cref='Array' /> that is the destination of the values copied from
////    ///     <see cref='InstructionCollection' /> .
////    /// </param>
////    /// <param name='index'>The index in <paramref name='array' /> where copying begins.</param>
////    /// <returns>
////    ///     None.
////    /// </returns>
////    /// <exception cref='ArgumentException'>
////    ///     <paramref name='array' /> is multidimensional. -or- The number of elements
////    ///     in the <see cref='InstructionCollection' /> is greater than the available space between <paramref name='index' />
////    ///     and the end of <paramref name='array' />.
////    /// </exception>
////    /// <exception cref='ArgumentNullException'><paramref name='array' /> is <see langword='null' />. </exception>
////    /// <exception cref='ArgumentOutOfRangeException'>
////    ///     <paramref name='index' /> is less than <paramref name='array' />'s
////    ///     lowbound.
////    /// </exception>
////    /// <seealso cref='Array' />
////    public void CopyTo(Instruction[] array, int index)
////    {
////        List.CopyTo(array, index);
////    }

////    /// <summary>
////    ///     Returns the index of a <see cref='Instruction' /> in
////    ///     the <see cref='InstructionCollection' /> .
////    /// </summary>
////    /// <param name='value'>The <see cref='Instruction' /> to locate.</param>
////    /// <returns>
////    ///     The index of the <see cref='Instruction' /> of <paramref name='value' /> in the
////    ///     <see cref='InstructionCollection' />, if found; otherwise, -1.
////    /// </returns>
////    /// <seealso cref='Contains' />
////    public int IndexOf(Instruction value)
////    {
////        return List.IndexOf(value);
////    }

////    /// <summary>
////    ///     Inserts a <see cref='Instruction' /> into the <see cref='InstructionCollection' /> at the specified index.
////    /// </summary>
////    /// <param name='index'>The zero-based index where <paramref name='value' /> should be inserted.</param>
////    /// <param name=' value'>The <see cref='Instruction' /> to insert.</param>
////    /// <returns>None.</returns>
////    /// <seealso cref='Add' />
////    public void Insert(int index, Instruction value)
////    {
////        List.Insert(index, value);
////    }

////    /// <summary>
////    ///     Returns an enumerator that can iterate through
////    ///     the <see cref='InstructionCollection' /> .
////    /// </summary>
////    /// <returns>None.</returns>
////    /// <seealso cref='IEnumerator' />
////    public new InstructionEnumerator GetEnumerator()
////    {
////        return new InstructionEnumerator(this);
////    }

////    /// <summary>
////    ///     Removes a specific <see cref='Instruction' /> from the
////    ///     <see cref='InstructionCollection' /> .
////    /// </summary>
////    /// <param name='value'>The <see cref='Instruction' /> to remove from the <see cref='InstructionCollection' /> .</param>
////    /// <returns>None.</returns>
////    /// <exception cref='ArgumentException'><paramref name='value' /> is not found in the Collection. </exception>
////    public void Remove(Instruction value)
////    {
////        List.Remove(value);
////    }

////    /// <summary>
////    ///     Provided for "foreach" support with this collection
////    /// </summary>
////    public class InstructionEnumerator : object, IEnumerator
////    {
////        private readonly IEnumerator baseEnumerator;

////        private readonly IEnumerable temp;

////        /// <summary>
////        ///     Public constructor for an InstructionEnumerator
////        /// </summary>
////        /// <param name="mappings">The <see cref="InstructionCollection" />we are going to iterate over</param>
////        public InstructionEnumerator(InstructionCollection mappings)
////        {
////            temp = mappings;
////            baseEnumerator = temp.GetEnumerator();
////        }

////        /// <summary>
////        ///     The current <see cref="Instruction" />
////        /// </summary>
////        public Instruction Current => (Instruction)baseEnumerator.Current;

////        /// <summary>
////        ///     The current IEnumerator interface
////        /// </summary>
////        object IEnumerator.Current => baseEnumerator.Current;

////        /// <summary>
////        ///     Move to the next Instruction
////        /// </summary>
////        /// <returns>true or false based on success</returns>
////        bool IEnumerator.MoveNext()
////        {
////            return baseEnumerator.MoveNext();
////        }

////        /// <summary>
////        ///     Reset the cursor
////        /// </summary>
////        void IEnumerator.Reset()
////        {
////            baseEnumerator.Reset();
////        }

////        /// <summary>
////        ///     Move to the next Instruction
////        /// </summary>
////        /// <returns>true or false based on success</returns>
////        public bool MoveNext()
////        {
////            return baseEnumerator.MoveNext();
////        }

////        /// <summary>
////        ///     Reset the cursor
////        /// </summary>
////        public void Reset()
////        {
////            baseEnumerator.Reset();
////        }
////    }
////}