using System;
using System.Collections;
using System.Collections.Generic;


namespace TinyOSCore.Cpu;

/// <summary>
///     A collection that stores <see cref='Instruction' /> objects.
/// </summary>
/// <seealso cref='Instructions' />
[Serializable]
public class Instructions : IEnumerable<Instruction>
{

    private List<Instruction> _instructions = new List<Instruction>();

    /// <summary>
    ///     Initializes a new instance of <see cref='Instructions' />.
    /// </summary>
    public Instructions()
    {
    }

    /// <summary>
    ///     Initializes a new instance of <see cref='Instructions' /> based on another
    ///     <see cref='Instructions' />.
    /// </summary>
    /// <param name='value'>
    ///     A <see cref='Instructions' /> from which the contents are copied
    /// </param>
    public Instructions(Instructions value)
    {
        AddRange(value);
    }

    /// <summary>
    ///     Adds a <see cref='Instruction' /> with the specified value to the
    ///     <see cref='Instructions' /> .
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
    ///     Adds the contents of another <see cref='Instructions' /> to the end of the collection.
    /// </summary>
    /// <param name='instructionCollection'>
    ///     A <see cref='Instructions' /> containing the objects to add to the collection.
    /// </param>
    /// <returns>
    ///     None.
    /// </returns>
    /// <seealso cref='Add' />
    public void AddRange(Instructions instructionCollection)
    {
        foreach (var t in instructionCollection)
        {
            Add(t);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the
    ///     <see cref='Instructions' /> contains the specified <see cref='Instruction' />.
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
    ///     the <see cref='Instructions' /> .
    /// </summary>
    /// <param name='value'>The <see cref='Instruction' /> to locate.</param>
    /// <returns>
    ///     The index of the <see cref='Instruction' /> of <paramref name='value' /> in the
    ///     <see cref='Instructions' />, if found; otherwise, -1.
    /// </returns>
    /// <seealso cref='Contains' />
    public int IndexOf(Instruction value)
    {
        return _instructions.IndexOf(value);
    }
    
    /// <summary>
    /// Provides enumeration capabilities so foreach looping can be used
    /// </summary>
    /// <returns>A strongly typed enumerator to the instruction collection</returns>
    IEnumerator<Instruction> IEnumerable<Instruction>.GetEnumerator()
    {
        return ((IEnumerable<Instruction>)_instructions).GetEnumerator();
    }

    /// <summary>
    /// Provides enumeration capabilities so foreach looping can be used 
    /// </summary>
    /// <returns>A weakly typed enumerator to the instruction collection</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_instructions).GetEnumerator();
    }
}
      