using System;
using System.Collections;

namespace TinyOSCore;

/// <summary>
///     Represents a running Process in the OS.runningProcesses table.  Implements
///     <see cref="IComparable" />
///     so two Processes can be compared with &gt; and &lt;.  This will allow easy sorting of the runningProcesses table
///     based on <see cref="ProcessControlBlock.Priority" />.
/// </summary>
public class Process : IComparable
{
    /// <summary>
    ///     Process Constructor
    /// </summary>
    /// <param name="processId">the readonly unique id for this Process</param>
    /// <param name="memorySize">the ammount of memory this Process and address</param>
    public Process(uint processId, uint memorySize)
    {
        PCB = new ProcessControlBlock(processId, memorySize);
    }

    /// <summary>
    /// </summary>
    public ProcessControlBlock PCB { get; set; }


    /// <summary>
    ///     Needed to implement <see cref="IComparable" />.  Compares Processes based on
    ///     <see cref="ProcessControlBlock.Priority" />.
    ///     <pre>
    ///         Value                  Meaning
    ///         --------------------------------------------------------
    ///         Less than zero         This instance is less than obj
    ///         Zero                   This instance is equal to obj
    ///         Greater than an zero   This instance is greater than obj
    ///     </pre>
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public int CompareTo(object obj)
    {
        if (obj is Process process)
        {
            //We want to sort HIGHEST priority first (reverse of typical)
            // Meaning 9,8,7,6,5,4,3,2,1 
            if (PCB.Priority < process.PCB.Priority)
            {
                return 1;
            }

            if (PCB.Priority > process.PCB.Priority)
            {
                return -1;
            }
            if (PCB.Priority == process.PCB.Priority)
            {
                //Make sure potentially starved processes get a chance
                if (PCB.ClockCycles < process.PCB.ClockCycles)
                {
                    return 1;
                }

                if (PCB.ClockCycles > process.PCB.ClockCycles)
                {
                    return -1;
                }
            }

            return 0;
        }

        throw new ArgumentException();
    }


    /// <summary>
    ///     Internal class to <see cref="Process" /> that represents a ProcessControlBlock.  It isn't a struct so it can have
    ///     instance field initializers.  Maintains things like <see cref="Registers" /> and <see cref="ClockCycles" /> for
    ///     this
    ///     Process.
    ///     Global Data Region at R9 and SP at R10 are set in <see cref="OS.CreateProcess" />
    /// </summary>
    public class ProcessControlBlock
    {
        /// <summary>
        ///     Constructor for a ProcessControlBlock
        /// </summary>
        /// <param name="id">the new readonly ProcessId.  Set only once, readonly afterwards.</param>
        /// <param name="memorySize"></param>
        public ProcessControlBlock(uint id, uint memorySize)
        {
            Pid = id;
            Registers[8] = Pid;
            ProcessMemorySize = memorySize;
        }

        #region Process Details

        /// <summary>
        ///     The OS-wide unique Process ID.  This is set in the <see cref="ProcessControlBlock" /> constructor.
        /// </summary>
        public uint Pid { get; }

        /// <summary>
        ///     The length of the code segement for this Process relative to the 0.  It points one byte after the code segment.
        /// </summary>
        public uint CodeSize { get; set; } = 0;

        /// <summary>
        ///     Maximum size of the stack for this Process
        /// </summary>
        public uint StackSize { get; set; } = 0;

        /// <summary>
        ///     Size of the Data Segement for this Process
        /// </summary>
        public uint DataSize { get; set; } = 0;

        /// <summary>
        ///     Start address of the Heap for this Process
        /// </summary>
        public uint HeapAddrStart { get; set; } = 0;

        /// <summary>
        ///     End Address of the Heap for this Process
        /// </summary>
        public uint HeapAddrEnd { get; set; } = 0;

        /// <summary>
        ///     ArrayList of MemoryPages that are associated with the Heap for this Process
        /// </summary>
        public ArrayList HeapPageTable { get; } = new ArrayList();

        /// <summary>
        ///     The ammount of memory this Process is allowed to access.
        /// </summary>
        public uint ProcessMemorySize { get; }

        #endregion

        #region Process State

        /// <summary>
        ///     The states this Process can go through.  Starts at NewProcess, changes to Running.
        /// </summary>
        public ProcessState State { get; set; } = ProcessState.NewProcess;

        /// <summary>
        ///     We have 10 registers.  R11 is the <see cref="InstructionPointer" />, and we don't use R0.  R10 is the
        ///     <see cref="StackPointer" />.  So, that's 1 to 10, and 11.
        /// </summary>
        public uint[] Registers { get; } = new uint[12];

        /// <summary>
        ///     We have a Sign Flag and a Zero Flag in a <see cref="BitArray" />
        /// </summary>
        private readonly BitArray _bitFlagRegisters = new BitArray(2, false);

        /// <summary>
        ///     This <see cref="Process">Process's</see> current priority.  Can be changed programmatically.
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        ///     The number of <see cref="ClockCycles" /> this <see cref="Process" /> can execute before being switched out.
        /// </summary>
        public int TimeQuantum { get; } = 5;

        /// <summary>
        ///     If we are waiting on a lock, we'll store it's value here
        /// </summary>
        public uint WaitingLock { get; set; } = 0;

        /// <summary>
        ///     If we are waiting on an event, we'll store it's value here
        /// </summary>
        public uint WaitingEvent { get; set; } = 0;

        #endregion

        #region Counter Variables

        /// <summary>
        ///     The number of clockCycles this <see cref="Process" /> has executed
        /// </summary>
        public int ClockCycles { get; set; } = 0;

        /// <summary>
        ///     The number of additional <see cref="ClockCycles" /> to sleep.
        ///     If we are in a waiting state, and this is 0, we will sleep forever.
        ///     If this is 1 (we are about to wake up) our state will change to ProcessState.Running
        /// </summary>
        public uint SleepCounter { get; set; } = 0;

        /// <summary>
        ///     The number of times this application has been switched out
        /// </summary>
        public int ContextSwitches { get; set; } = 0;

        /// <summary>
        ///     The number of pageFaults this <see cref="Process" /> has experienced.
        /// </summary>
        public int PageFaults { get; } = 0;

        #endregion

        #region Accessors

        /// <summary>
        ///     Public get/set accessor for the Sign Flag
        /// </summary>
        public bool SignFlag //Sign Flag
        {
            get => _bitFlagRegisters[0];
            set => _bitFlagRegisters[0] = value;
        }

        /// <summary>
        ///     Public get/set accessor for the Zero Flag
        /// </summary>
        public bool ZeroFlag //Zero Flag
        {
            get => _bitFlagRegisters[1];
            set => _bitFlagRegisters[1] = value;
        }

        /// <summary>
        ///     Public get/set accessor for the Stack Pointer
        /// </summary>
        public uint StackPointer
        {
            get => Registers[10];
            set => Registers[10] = value;
        }

        /// <summary>
        ///     Public get/set accessor for the Instruction Pointer
        /// </summary>
        public uint InstructionPointer
        {
            get => Registers[11];
            set => Registers[11] = value;
        }

        #endregion
    }
}

#region Process-specific enums

/// <summary>
///     All the states a <see cref="Process" /> can experience.
/// </summary>
public enum ProcessState
{
    /// <summary>
    ///     A <see cref="Process" /> initial state
    /// </summary>
    NewProcess = 0,

    /// <summary>
    ///     The state of a <see cref="Process" /> ready to run
    /// </summary>
    Ready,

    /// <summary>
    ///     The state of the currently running <see cref="Process" />
    /// </summary>
    Running,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting after a Sleep
    /// </summary>
    WaitingAsleep,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting after an AcquireLock
    /// </summary>
    WaitingOnLock,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting after a WaitEvent
    /// </summary>
    WaitingOnEvent,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting to be removed from the Running <see cref="ProcessCollection" />
    /// </summary>
    Terminated
}

/// <summary>
///     The Range of <see cref="Process.ProcessControlBlock.Priority" /> a <see cref="Process" /> can experience.
/// </summary>
public enum ProcessPriority
{
    /// <summary>
    ///     The lowest priority a <see cref="Process" /> can be
    /// </summary>
    LowPriority = 0,

    /// <summary>
    ///     The Highest priority a <see cref="Process" /> can be
    /// </summary>
    MaxPriority = 31
}

#endregion