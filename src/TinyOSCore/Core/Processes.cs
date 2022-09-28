using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TinyOSCore.Core
{
    /// <summary>
	///     
	///       A collection that stores <see cref='Process'/> objects.
	///    
	/// </summary>
	/// <seealso cref='Processes'/>
	[Serializable]
    public class Processes :   IEnumerable<Process>
    {

        private List<Process> _processes = new List<Process>();
        /// <summary>
        ///     
        ///       Initializes a new instance of <see cref='Processes'/>.
        ///    
        /// </summary>
        public Processes()
        {
        }

        /// <summary>
        /// Represents the entry at the specified index of the <see cref='Process'/>.
        /// </summary>
        /// <param name='index'>The zero-based index of the entry to locate in the collection.</param>
        /// <value>
        ///     The entry at the specified index of the collection.
        /// </value>
        /// <exception cref='ArgumentOutOfRangeException'><paramref name='index'/> is outside the valid range of indexes for the collection.</exception>
        public Process this[int index]
        {
            get => _processes[index];
            set => _processes[index] = value;
        }

        /// <summary>
        ///    Adds a <see cref='Process'/> with the specified value to the 
        ///    <see cref='Processes'/> .
        /// </summary>
        /// <param name='process'>The <see cref='Process'/> to add.</param>
        /// <returns>
        ///    The index at which the new element was inserted.
        /// </returns>
        public int Add(Process process)
        {
            _processes.Add(process);
            return _processes.Count;
        }

        
        /// <summary>
        /// Gets a value indicating whether the 
        ///    <see cref='Processes'/> contains the specified <see cref='Process'/>.
        /// </summary>
        /// <param name='process'>The <see cref='Process'/> to locate.</param>
        /// <returns>
        /// <see langword='true'/> if the <see cref='Process'/> is contained in the collection; 
        ///   otherwise, <see langword='false'/>.
        /// </returns>
        /// <seealso cref='IndexOf'/>
        public bool Contains(Process process)
        {
            return _processes.Contains(process);
        }


        /// <summary>
        /// Number of processes in the process list
        /// </summary>
        public int Count => _processes.Count;

        /// <summary>
        /// Sorts the list of <see cref="OS.RunningProcesses"/> based on <see cref="Process.ProcessControlBlock.Priority"/>
        /// </summary>
        public void Sort()
        {
            _processes = _processes.OrderByDescending(p => p.ProcessControlBlock.Priority).ThenBy(p=>p.ProcessControlBlock.ClockCycles).ToList();
        }

        
        /// <summary>
        ///    Returns the index of a <see cref='Process'/> in 
        ///       the <see cref='Processes'/> .
        /// </summary>
        /// <param name='process'>The <see cref='Process'/> to locate.</param>
        /// <returns>
        /// The index of the <see cref='Process'/> of <paramref name='process'/> in the 
        /// <see cref='Processes'/>, if found; otherwise, -1.
        /// </returns>
        /// <seealso cref='Contains'/>
        public int IndexOf(Process process)
        {
            return _processes.IndexOf(process);
        }

        /// <summary>
        /// Inserts a <see cref='Process'/> into the <see cref='Processes'/> at the specified index.
        /// </summary>
        /// <param name='index'>The zero-based index where <paramref name='process'/> should be inserted.</param>
        /// <param name='process'>The <see cref='Process'/> to insert.</param>
        /// <returns>None.</returns>
        /// <seealso cref='Add'/>
        public void Insert(int index, Process process)
        {
            _processes.Insert(index, process);
        }

        /// <summary>
        ///     Removes a specific <see cref='Process'/> from the 
        ///    <see cref='Processes'/> .
        /// </summary>
        /// <param name='process'>The <see cref='Process'/> to remove from the <see cref='Processes'/> .</param>
        /// <returns>None.</returns>
        /// <exception cref='ArgumentException'><paramref name='process'/> is not found in the Collection. </exception>
        public void Remove(Process process)
        {
            _processes.Remove(process);
        }

        /// <summary>
        /// Remove a process from the processes at position i
        /// </summary>
        /// <param name="i">position of process to remove</param>
        public void RemoveAt(int i)
        {
            var process = _processes[i];
            _processes.Remove(process);
        }

        /// <summary>
        /// Provides enumeration capabilities so foreach looping can be used
        /// </summary>
        /// <returns>A strongly typed enumerator to the instruction collection</returns>
        IEnumerator<Process> IEnumerable<Process>.GetEnumerator()
        {
            return ((IEnumerable<Process>)_processes).GetEnumerator();
        }

        /// <summary>
        /// Provides enumeration capabilities so foreach looping can be used 
        /// </summary>
        /// <returns>A weakly typed enumerator to the instruction collection</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_processes).GetEnumerator();
        }
    }
}
