using System;
using System.Collections;

namespace TinyOSCore.Core
{
    /// <summary>
	///     
	///       A collection that stores <see cref='Process'/> objects.
	///    
	/// </summary>
	/// <seealso cref='ProcessCollection'/>
	[Serializable()]
    public class ProcessCollection : CollectionBase, IComparer
    {

        /// <summary>
        ///     
        ///       Initializes a new instance of <see cref='ProcessCollection'/>.
        ///    
        /// </summary>
        public ProcessCollection()
        {
        }

        /// <summary>
        ///     
        ///       Initializes a new instance of <see cref='ProcessCollection'/> based on another <see cref='ProcessCollection'/>.
        ///    
        /// </summary>
        /// <param name='value'>
        ///       A <see cref='ProcessCollection'/> from which the contents are copied
        /// </param>
        public ProcessCollection(ProcessCollection value)
        {
            AddRange(value);
        }

        /// <summary>
        ///     
        ///       Initializes a new instance of <see cref='ProcessCollection'/> containing any array of <see cref='Process'/> objects.
        ///    
        /// </summary>
        /// <param name='value'>
        ///       A array of <see cref='Process'/> objects with which to intialize the collection
        /// </param>
        public ProcessCollection(Process[] value)
        {
            AddRange(value);
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
            get => (Process)List[index];
            set => List[index] = value;
        }

        /// <summary>
        ///    Adds a <see cref='Process'/> with the specified value to the 
        ///    <see cref='ProcessCollection'/> .
        /// </summary>
        /// <param name='value'>The <see cref='Process'/> to add.</param>
        /// <returns>
        ///    The index at which the new element was inserted.
        /// </returns>
        /// <seealso cref='AddRange(ProcessCollection)'/>
        public int Add(Process value)
        {
            return List.Add(value);
        }

        /// <summary>
        /// Copies the elements of an array to the end of the <see cref='ProcessCollection'/>.
        /// </summary>
        /// <param name='processes'>
        ///    An array of type <see cref='Process'/> containing the objects to add to the collection.
        /// </param>
        /// <returns>
        ///   None.
        /// </returns>
        /// <seealso cref='Add'/>
        public void AddRange(Process[] processes)
        {
            foreach (var process in processes)
            {
                Add(process);
            }
        }

        /// <summary>
        ///     
        ///       Adds the contents of another <see cref='ProcessCollection'/> to the end of the collection.
        ///    
        /// </summary>
        /// <param name='processes'>
        ///    A <see cref='ProcessCollection'/> containing the objects to add to the collection.
        /// </param>
        /// <returns>
        ///   None.
        /// </returns>
        /// <seealso cref='Add'/>
        public void AddRange(ProcessCollection processes)
        {
            foreach (var t in processes)
            {
                Add(t);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the 
        ///    <see cref='ProcessCollection'/> contains the specified <see cref='Process'/>.
        /// </summary>
        /// <param name='value'>The <see cref='Process'/> to locate.</param>
        /// <returns>
        /// <see langword='true'/> if the <see cref='Process'/> is contained in the collection; 
        ///   otherwise, <see langword='false'/>.
        /// </returns>
        /// <seealso cref='IndexOf'/>
        public bool Contains(Process process)
        {
            return List.Contains(process);
        }


        /// <summary>
        /// Implemented for IComparable.  
        /// </summary>
        /// <param name="x">A Process object</param>
        /// <param name="y">A Process object</param>
        /// <returns>a comparison int from CompareTo</returns>
        public int Compare(object x, object y)
        {
            var px = (Process)x;
            var py = (Process)y;

            return px.CompareTo(py);
        }

        /// <summary>
        /// Sorts the list of <see cref="OS.RunningProcesses"/> based on <see cref="Process.ProcessControlBlock.Priority"/>
        /// </summary>
        public void Sort()
        {
            InnerList.Sort(this);
        }

        /// <summary>
        /// Copies the <see cref='ProcessCollection'/> values to a one-dimensional <see cref='Array'/> instance at the 
        ///    specified index.
        /// </summary>
        /// <param name='processes'>The one-dimensional <see cref='Array'/> that is the destination of the values copied from <see cref='ProcessCollection'/> .</param>
        /// <param name='index'>The index in <paramref name='processes'/> where copying begins.</param>
        /// <returns>
        ///   None.
        /// </returns>
        /// <exception cref='ArgumentException'><paramref name='processes'/> is multidimensional. -or- The number of elements in the <see cref='ProcessCollection'/> is greater than the available space between <paramref name='index'/> and the end of <paramref name='processes'/>.</exception>
        /// <exception cref='ArgumentNullException'><paramref name='processes'/> is <see langword='null'/>. </exception>
        /// <exception cref='ArgumentOutOfRangeException'><paramref name='index'/> is less than <paramref name='processes'/>'s lowbound. </exception>
        /// <seealso cref='Array'/>
        public void CopyTo(Process[] processes, int index)
        {
            List.CopyTo(processes, index);
        }

        /// <summary>
        ///    Returns the index of a <see cref='Process'/> in 
        ///       the <see cref='ProcessCollection'/> .
        /// </summary>
        /// <param name='process'>The <see cref='Process'/> to locate.</param>
        /// <returns>
        /// The index of the <see cref='Process'/> of <paramref name='process'/> in the 
        /// <see cref='ProcessCollection'/>, if found; otherwise, -1.
        /// </returns>
        /// <seealso cref='Contains'/>
        public int IndexOf(Process process)
        {
            return List.IndexOf(process);
        }

        /// <summary>
        /// Inserts a <see cref='Process'/> into the <see cref='ProcessCollection'/> at the specified index.
        /// </summary>
        /// <param name='index'>The zero-based index where <paramref name='process'/> should be inserted.</param>
        /// <param name=' process'>The <see cref='Process'/> to insert.</param>
        /// <returns>None.</returns>
        /// <seealso cref='Add'/>
        public void Insert(int index, Process process)
        {
            List.Insert(index, process);
        }

        /// <summary>
        ///    Returns an enumerator that can iterate through 
        ///       the <see cref='ProcessCollection'/> .
        /// </summary>
        /// <returns>None.</returns>
        /// <seealso cref='IEnumerator'/>
        public new ProcessEnumerator GetEnumerator()
        {
            return new ProcessEnumerator(this);
        }

        /// <summary>
        ///     Removes a specific <see cref='Process'/> from the 
        ///    <see cref='ProcessCollection'/> .
        /// </summary>
        /// <param name='process'>The <see cref='Process'/> to remove from the <see cref='ProcessCollection'/> .</param>
        /// <returns>None.</returns>
        /// <exception cref='ArgumentException'><paramref name='process'/> is not found in the Collection. </exception>
        public void Remove(Process process)
        {
            List.Remove(process);
        }

        /// <summary>
        /// Provided for "foreach" support with this collection
        /// </summary>
        public class ProcessEnumerator : object, IEnumerator
        {

            private readonly IEnumerator _baseEnumerator;

            private readonly IEnumerable _temp;

            /// <summary>
            /// Public constructor for an ProcessEnumerator
            /// </summary>
            /// <param name="mappings">The <see cref="ProcessCollection"/>we are going to iterate over</param>
            public ProcessEnumerator(ProcessCollection mappings)
            {
                _temp = mappings;
                _baseEnumerator = _temp.GetEnumerator();
            }

            /// <summary>
            /// The current <see cref="Process"/>
            /// </summary>
            public Process Current => (Process)_baseEnumerator.Current;

            /// <summary>
            /// The current IEnumerator interface
            /// </summary>
            object IEnumerator.Current => _baseEnumerator.Current;

            /// <summary>
            /// Move to the next Process
            /// </summary>
            /// <returns>true or false based on success</returns>
            public bool MoveNext()
            {
                return _baseEnumerator.MoveNext();
            }

            /// <summary>
            /// Move to the next Process
            /// </summary>
            /// <returns>true or false based on success</returns>
            bool IEnumerator.MoveNext()
            {
                return _baseEnumerator.MoveNext();
            }

            /// <summary>
            /// Reset the cursor
            /// </summary>
            public void Reset()
            {
                _baseEnumerator.Reset();
            }

            /// <summary>
            /// Reset the cursor
            /// </summary>
            void IEnumerator.Reset()
            {
                _baseEnumerator.Reset();
            }
        }
    }
}
