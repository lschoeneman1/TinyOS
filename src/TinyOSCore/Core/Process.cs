using System;

namespace TinyOSCore.Core;

/// <summary>
///     Represents a running Process in the <see cref="OS.RunningProcesses" /> table.  Implements
///     <see cref="IComparable" />
///     so two Processes can be compared with &gt; and &lt;.  This will allow easy sorting of the runningProcesses table
///     based on <see cref="Core.ProcessControlBlock.Priority" />.
/// </summary>
public class Process : IComparable
{
    /// <summary>
    /// </summary>
    public ProcessControlBlock ProcessControlBlock { get; }

    /// <summary>
    ///     Process Constructor
    /// </summary>
    /// <param name="processId">the readonly unique id for this Process</param>
    /// <param name="memorySize">the ammount of memory this Process and address</param>
    public Process(uint processId, uint memorySize)
    {
        ProcessControlBlock = new ProcessControlBlock(processId, memorySize);
    }


    /// <summary>
    ///     Needed to implement <see cref="IComparable" />.  Compares Processes based on
    ///     <see cref="Core.ProcessControlBlock.Priority" />.
    ///     <pre>
    ///         Value                  Meaning
    ///         --------------------------------------------------------
    ///         Less than zero         This instance is less than obj
    ///         Zero                   This instance is equal to obj
    ///         Greater than an zero   This instance is greater than obj
    ///     </pre>
    /// </summary>
    /// <param name="obj"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns></returns>
    public int CompareTo(object obj)
    {
        if (obj is Process process)
        {
            //We want to sort HIGHEST priority first (reverse of typical)
            // Meaning 9,8,7,6,5,4,3,2,1 
            if (ProcessControlBlock.Priority < process.ProcessControlBlock.Priority)
            {
                return 1;
            }

            if (ProcessControlBlock.Priority > process.ProcessControlBlock.Priority)
            {
                return -1;
            }

            if (ProcessControlBlock.Priority == process.ProcessControlBlock.Priority)
            {
                //Make sure potentially starved processes get a chance
                if (ProcessControlBlock.ClockCycles < process.ProcessControlBlock.ClockCycles)
                {
                    return 1;
                }

                if (ProcessControlBlock.ClockCycles > process.ProcessControlBlock.ClockCycles)
                {
                    return -1;
                }
            }

            return 0;
        }

        throw new ArgumentException();
    }


}