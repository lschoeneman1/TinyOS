using System;
using System.Diagnostics;
using TinyOSCore.Cpu;
using TinyOSCore.Exceptions;
using TinyOSCore.MemoryManagement;

namespace TinyOSCore.Core;

/// <summary>
///     The delegate (object-oriented function pointer) definition for an OS System Call.
///     ALl opCodes will be mapped to a function that matches this signature
/// </summary>
public delegate void SystemCall();

/// <summary>
///     The definition of an Operarting System, including a <see cref="MemoryManager" /> and a
///     <see cref="Processes" />
/// </summary>
public class OS
{
    

    /// <summary>
    ///     Do we output debug for Instructions?
    /// </summary>
    private readonly bool _dumpInstructions;

    /// <summary>
    ///     Contains the <see cref="Process" /> and the <see cref="Process.ProcessControlBlock" /> for all RunningProcesses
    /// </summary>
    public Processes RunningProcesses { get; } = new Processes();

    /// <summary>
    ///     Public constructor for the OS
    /// </summary>
    /// <param name="virtualMemoryBytes">The number of "addressable" bytes of memory for the whole OS.</param>
    public OS(uint virtualMemoryBytes)
    {
        MemoryMgr = new MemoryManager(virtualMemoryBytes);
        _dumpInstructions = bool.Parse(EntryPoint.Configuration["DumpInstruction"]);
    }

    /// <summary>
    ///     Holds a reference to the current running <see cref="Process" />
    /// </summary>
    public Process CurrentProcess { get; set; }

    /// <summary>
    ///     A reference to the <see cref="MemoryManager" /> Class.  A <see cref="Process" /> memory accesses go
    ///     through this class.
    /// </summary>
    /// <example>
    ///     theOS.memoryMgr[processId, 5]; //accesses memory at address 5
    /// </example>
    public MemoryManager MemoryMgr { get; }

    /// <summary>
    ///     There are 10 Locks, numbered 1 to 10.  Lock 0 is not used.
    ///     We will store 0 when the lock is free, or the ProcessID when the lock is acquired
    /// </summary>
    public uint[] Locks { get; } = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>
    ///     There are 10 Events, numbered 1 to 10.  Event 0 is not used
    /// </summary>
    public EventState[] Events { get; } = new EventState[11];

    /// <summary>
    ///     This counter is incremented as new processes are created.
    ///     It provides a unique id for a process. Process Id 0 is assumed to be the OS.
    /// </summary>
    public static uint ProcessIdPool { get; set; }

    /// <summary>
    ///     Checks if the <see cref="CurrentProcess" /> is eligible to run
    /// </summary>
    /// <returns>true if the <see cref="CurrentProcess" /> is eligible to run</returns>
    public bool CurrentProcessIsEligible()
    {
        if (CurrentProcess == null)
        {
            return false;
        }

        return CurrentProcess.ProcessControlBlock.State != ProcessState.Terminated 
               && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnLock 
               && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingAsleep &&
               CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnEvent;
    }

    /// <summary>
    ///     Dumps collected statistics of a process when it's been removed from the <see cref="RunningProcesses" /> table
    /// </summary>
    /// <param name="processIndex">The Index (not the ProcessID!) in the <see cref="RunningProcesses" /> table of a Process</param>
    public void DumpProcessStatistics(int processIndex)
    {
        var process = RunningProcesses[processIndex];

        Console.WriteLine($"Removed Exited Process # {process.ProcessControlBlock.Pid}");
        Console.WriteLine($"  # of Page Faults:      {MemoryMgr.PageFaultsForProcess(process)}");
        Console.WriteLine($"  # of Clock Cycles:     {process.ProcessControlBlock.ClockCycles}");
        Console.WriteLine($"  # of Context Switches: {process.ProcessControlBlock.ContextSwitches}");
    }

    /// <summary>
    ///     Releases any Locks held by this process.
    ///     This function is called when the process exits.
    /// </summary>
    /// <param name="pid">Process ID</param>
    public void ReleaseLocksOfProccess(uint pid)
    {
        for (var i = 0; i < Locks.Length; i++)
        {
            if (Locks[i] == pid)
            {
                Locks[i] = 0;
            }
        }
    }

    public void execute()
    {
        while (true)
        {
            //
            // Yank terminated processes
            //
            for (int i = RunningProcesses.Count - 1; i >= 0; i--)
            {
                if (RunningProcesses[i].ProcessControlBlock.State == ProcessState.Terminated)
                {
                    DumpProcessStatistics(i);
                    MemoryMgr.ReleaseMemoryOfProcess(RunningProcesses[i].ProcessControlBlock.Pid);
                    RunningProcesses[i].ProcessControlBlock.HeapPageTable.Clear();
                    ReleaseLocksOfProccess(RunningProcesses[i].ProcessControlBlock.Pid);
                    RunningProcesses.RemoveAt(i);
                    CPU.DumpPhysicalMemory();
                }
            }

            // Sort high priority first + least used clock cycles first to avoid starvation
            // see Process.Compare
            // 
            RunningProcesses.Sort();

            if (RunningProcesses.Count == 0)
            {
                Console.WriteLine("No Processes");
                if (bool.Parse(EntryPoint.Configuration["PauseOnExit"]) == true) System.Console.ReadLine();
                System.Environment.Exit(0);
            }
            else
            {
                foreach (Process p in RunningProcesses)
                {
                    switch (p.ProcessControlBlock.State)
                    {
                        case ProcessState.Terminated:
                            //yank old processes outside the foreach
                            break;
                        case ProcessState.WaitingAsleep:
                            //is this process waiting for an event?
                            break;
                        case ProcessState.WaitingOnLock:
                            //is this process waiting for an event?
                            break;
                        case ProcessState.WaitingOnEvent:
                            //is this process waiting for an event?
                            break;
                        case ProcessState.NewProcess:
                        case ProcessState.Ready:
                            CurrentProcess = p;

                            //copy state from ProcessControlBlock to CPU
                            LoadCPUState();

                            DumpContextSwitchIn();

                            // Reset this flag. If we need to interrupt execution 
                            // because a lock has been made available
                            // or an Event has signaled, we can preempt the current process
                            bool bPreemptCurrentProcess = false;

                            while (CurrentProcessIsEligible())
                            {
                                CurrentProcess.ProcessControlBlock.State = ProcessState.Running;

                                //CPU.DumpPhysicalMemory();
                                //CPU.DumpRegisters();

                                try
                                {
                                    CPU.ExecuteNextOpCode();
                                    CurrentProcess.ProcessControlBlock.ClockCycles++;
                                }
                                catch (MemoryException e)
                                {
                                    Console.WriteLine(e.ToString());
                                    CPU.DumpRegisters();
                                    CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
                                }
                                catch (StackException e)
                                {
                                    Console.WriteLine(e.ToString());
                                    CPU.DumpRegisters();
                                    CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
                                }
                                catch (HeapException e)
                                {
                                    Console.WriteLine(e.ToString());
                                    CPU.DumpRegisters();
                                    CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
                                }

                                CPU.DumpPhysicalMemory();
                                CPU.DumpRegisters();

                                //
                                // Update any sleeping processes
                                //
                                foreach (Process sleepingProcess in RunningProcesses)
                                {
                                    switch (sleepingProcess.ProcessControlBlock.State)
                                    {
                                        case ProcessState.WaitingAsleep:
                                            // a SleepCounter of 0 sleeps forever if we are waiting
                                            if (sleepingProcess.ProcessControlBlock.SleepCounter != 0)
                                                //If we JUST reached 0, wake up!
                                                if (--sleepingProcess.ProcessControlBlock.SleepCounter == 0)
                                                {
                                                    sleepingProcess.ProcessControlBlock.State = ProcessState.Ready;
                                                    bPreemptCurrentProcess = true;
                                                }
                                            break;
                                        case ProcessState.WaitingOnEvent:
                                            // Are we waiting for an event?  We'd better be!
                                            Debug.Assert(sleepingProcess.ProcessControlBlock.WaitingEvent != 0);

                                            // Had the event been signalled recently?
                                            if (this.Events[sleepingProcess.ProcessControlBlock.WaitingEvent] == EventState.Signaled)
                                            {
                                                this.Events[sleepingProcess.ProcessControlBlock.WaitingEvent] = EventState.NonSignaled;
                                                sleepingProcess.ProcessControlBlock.State = ProcessState.Ready;
                                                sleepingProcess.ProcessControlBlock.WaitingEvent = 0;
                                                bPreemptCurrentProcess = true;
                                            }
                                            break;
                                        case ProcessState.WaitingOnLock:
                                            // We are are in the WaitingOnLock state, we can't wait on the "0" lock
                                            Debug.Assert(sleepingProcess.ProcessControlBlock.WaitingLock != 0);

                                            // Has the lock be released recently?
                                            if (this.Locks[sleepingProcess.ProcessControlBlock.WaitingLock] == 0)
                                            {
                                                // Acquire the Lock and wake up!
                                                this.Locks[sleepingProcess.ProcessControlBlock.WaitingLock] = sleepingProcess.ProcessControlBlock.WaitingLock;
                                                sleepingProcess.ProcessControlBlock.State = ProcessState.Ready; bPreemptCurrentProcess = true;
                                                sleepingProcess.ProcessControlBlock.WaitingLock = 0;
                                                bPreemptCurrentProcess = true;
                                            }
                                            break;
                                    }
                                }

                                // Have we used up our slice of time?
                                bool bEligible = CurrentProcess.ProcessControlBlock.ClockCycles == 0 || (CurrentProcess.ProcessControlBlock.ClockCycles % CurrentProcess.ProcessControlBlock.TimeQuantum != 0);
                                if (!bEligible)
                                    break;
                                if (bPreemptCurrentProcess)
                                    break;
                            }
                            if (CurrentProcess.ProcessControlBlock.State != ProcessState.Terminated)
                            {
                                //copy state from CPU to ProcessControlBlock
                                if (CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingAsleep
                                    && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnLock
                                    && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnEvent)
                                    CurrentProcess.ProcessControlBlock.State = ProcessState.Ready;
                                CurrentProcess.ProcessControlBlock.ContextSwitches++;

                                DumpContextSwitchOut();

                                SaveCPUState();

                                //Clear Registers for testing
                                CPU.Registers = new uint[12];
                            }
                            CurrentProcess = null;
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     The primary control loop for the whole OS.
    ///     Spins through eligible processes and executes their opCodes
    ///     Provides scheduling and removes old processes.
    /// </summary>
    public void Execute()
    {
//        return;
        while (true)
        {
            //
            // Yank terminated processes
            //
            for (var i = RunningProcesses.Count - 1; i >= 0; i--)
            {
                if (RunningProcesses[i].ProcessControlBlock.State == ProcessState.Terminated)
                {
                    DumpProcessStatistics(i);
                    MemoryMgr.ReleaseMemoryOfProcess(RunningProcesses[i].ProcessControlBlock.Pid);
                    RunningProcesses[i].ProcessControlBlock.HeapPageTable.Clear();
                    ReleaseLocksOfProccess(RunningProcesses[i].ProcessControlBlock.Pid);
                    RunningProcesses.RemoveAt(i);
                    CPU.DumpPhysicalMemory();
                }
            }

            // Sort high priority first + least used clock cycles first to avoid starvation
            // see Process.Compare
            // 
            RunningProcesses.Sort();

            if (RunningProcesses.Count == 0)
            {
                Console.WriteLine("No Processes");
                if (bool.Parse(EntryPoint.Configuration["PauseOnExit"]))
                {
                    Console.ReadLine();
                }

                Environment.Exit(0);
            }
            else
            {
                HandleActiveProcesses();
            }
        }
    }

    private void HandleActiveProcesses()
    {
        foreach (var process in RunningProcesses)
        {
            if (process.ProcessControlBlock.State is ProcessState.NewProcess or ProcessState.Ready)
            {
                HandleRunningAndReadyProcesses(process);
            }
        }
    }

    private void HandleRunningAndReadyProcesses(Process process)
    {
        CurrentProcess = process;

        //copy state from ProcessControlBlock to CPU
        LoadCPUState();

        DumpContextSwitchIn();

        // Reset this flag. If we need to interrupt execution 
        // because a lock has been made available
        // or an Event has signaled, we can preempt the current process
        var preemptCurrentProcess = false;

        while (CurrentProcessIsEligible())
        {
            CurrentProcess.ProcessControlBlock.State = ProcessState.Running;

            ExecuteNextOpCode();

            //
            // Update any sleeping processes
            //
            foreach (var sleepingProcess in RunningProcesses)
            {
                if (sleepingProcess.ProcessControlBlock.State == ProcessState.WaitingAsleep)
                {
                    // a SleepCounter of 0 sleeps forever if we are waiting
                    //If we JUST reached 0, wake up!
                    if (sleepingProcess.ProcessControlBlock.SleepCounter != 0 
                        && --sleepingProcess.ProcessControlBlock.SleepCounter == 0)
                    {
                        sleepingProcess.ProcessControlBlock.State = ProcessState.Ready;
                        preemptCurrentProcess = true;
                    }
                }
                else if (sleepingProcess.ProcessControlBlock.State == ProcessState.WaitingOnEvent)
                {
                    // Are we waiting for an event?  We'd better be!
                    Debug.Assert(sleepingProcess.ProcessControlBlock.WaitingEvent != 0);

                    // Had the event been signalled recently?
                    if (Events[sleepingProcess.ProcessControlBlock.WaitingEvent] == EventState.Signaled)
                    {
                        Events[sleepingProcess.ProcessControlBlock.WaitingEvent] = EventState.NonSignaled;
                        sleepingProcess.ProcessControlBlock.State = ProcessState.Ready;
                        sleepingProcess.ProcessControlBlock.WaitingEvent = 0;
                        preemptCurrentProcess = true;
                    }
                }
                else if (sleepingProcess.ProcessControlBlock.State == ProcessState.WaitingOnLock)
                {
                    // We are are in the WaitingOnLock state, we can't wait on the "0" lock
                    Debug.Assert(sleepingProcess.ProcessControlBlock.WaitingLock != 0);

                    // Has the lock be released recently?
                    if (Locks[sleepingProcess.ProcessControlBlock.WaitingLock] == 0)
                    {
                        // Acquire the Lock and wake up!
                        Locks[sleepingProcess.ProcessControlBlock.WaitingLock] =
                            sleepingProcess.ProcessControlBlock.WaitingLock;
                        sleepingProcess.ProcessControlBlock.State = ProcessState.Ready;
                        preemptCurrentProcess = true;
                        sleepingProcess.ProcessControlBlock.WaitingLock = 0;
                        preemptCurrentProcess = true;
                    }
                }
            }

            // Have we used up our slice of time?
            var eligible = CurrentProcess.ProcessControlBlock.ClockCycles == 0 
                                || CurrentProcess.ProcessControlBlock.ClockCycles % CurrentProcess.ProcessControlBlock.TimeQuantum != 0;
            if (!eligible || preemptCurrentProcess)
            {
                break;
            }

        }

        if (CurrentProcess.ProcessControlBlock.State != ProcessState.Terminated)
        {
            //copy state from CPU to ProcessControlBlock
            if (CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingAsleep
                && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnLock
                && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnEvent)
            {
                CurrentProcess.ProcessControlBlock.State = ProcessState.Ready;
            }

            CurrentProcess.ProcessControlBlock.ContextSwitches++;

            DumpContextSwitchOut();

            SaveCPUState();

            //Clear Registers for testing
            CPU.Registers = new uint[12];
        }

        CurrentProcess = null;
    }

    /// <summary>
    /// Execute the next opcode and update the state appriopriately if errors occur
    /// </summary>
    private void ExecuteNextOpCode()
    {
        try
        {
            CPU.ExecuteNextOpCode();
            CurrentProcess.ProcessControlBlock.ClockCycles++;
        }
        catch (MemoryException e)
        {
            Console.WriteLine(e.ToString());
            CPU.DumpRegisters();
            CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
        }
        catch (StackException e)
        {
            Console.WriteLine(e.ToString());
            CPU.DumpRegisters();
            CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
        }
        catch (HeapException e)
        {
            Console.WriteLine(e.ToString());
            CPU.DumpRegisters();
            CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
        }

        CPU.DumpPhysicalMemory();
        CPU.DumpRegisters();
    }

    /// <summary>
    ///     If the DumpContextSwitch Configuration option is set to True, reports the Context Switch.
    ///     Used for debugging
    /// </summary>
    public void DumpContextSwitchIn()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpContextSwitch"]) == false)
        {
            return;
        }

        Console.WriteLine($"Switching in Process {CurrentProcess.ProcessControlBlock.Pid} with ip at {CurrentProcess.ProcessControlBlock.InstructionPointer}");
    }

    /// <summary>
    ///     If the DumpContextSwitch Configuration option is set to True, reports the Context Switch.
    ///     Used for debugging
    /// </summary>
    public void DumpContextSwitchOut()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpContextSwitch"]) == false)
        {
            return;
        }

        Console.WriteLine($"Switching out Process {CurrentProcess.ProcessControlBlock.Pid} with ip at {CPU.InstructionPointer}");
    }

    /// <summary>
    ///     Outputs a view of memory from the Process's point of view
    /// </summary>
    /// <param name="process">The Process to Dump</param>
    public void DumpProcessMemory(Process process)
    {
        var address = 0;
        for (uint i = 0; i < process.ProcessControlBlock.ProcessMemorySize; i++)
        {
            var b = MemoryMgr[process.ProcessControlBlock.Pid, i];
            if (address == 0 || address % 16 == 0)
            {
                Console.Write($"{Environment.NewLine}{address,-4:000} ");
            }

            address++;
            Console.Write(b == 0 ? $"{"-",3}" : $"{(int)b,3}");

            if (address % 4 == 0 && address % 16 != 0)
            {
                Console.Write("  :");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    ///     Called on a context switch. Copy the CPU's <see cref="CPU.Registers" /> to the <see cref="CurrentProcess" />'s
    ///     <see cref="CPU.Registers" />
    /// </summary>
    private void SaveCPUState()
    {
        CPU.Registers.CopyTo(CurrentProcess.ProcessControlBlock.Registers, 0);
        CurrentProcess.ProcessControlBlock.ZeroFlag = CPU.ZeroFlag;
        CurrentProcess.ProcessControlBlock.SignFlag = CPU.SignFlag;
        CurrentProcess.ProcessControlBlock.InstructionPointer = CPU.InstructionPointer;
    }

    /// <summary>
    ///     Called on a context switch. Copy the <see cref="CurrentProcess" />'s
    ///     <see cref="Process.ProcessControlBlock" /> to the CPU's <see cref="CPU.Registers" />
    /// </summary>
    private void LoadCPUState()
    {
        CurrentProcess.ProcessControlBlock.Registers.CopyTo(CPU.Registers, 0);
        CPU.ZeroFlag = CurrentProcess.ProcessControlBlock.ZeroFlag;
        CPU.SignFlag = CurrentProcess.ProcessControlBlock.SignFlag;
        CPU.InstructionPointer = CurrentProcess.ProcessControlBlock.InstructionPointer;
    }

    /// <summary>
    ///     Take as a <see cref="Program" /> and creates a Process object, adding it to the <see cref="RunningProcesses" />
    /// </summary>
    /// <param name="prog">Program to load</param>
    /// <param name="memorySize">Size of memory in bytes to assign to this Process</param>
    /// <returns>The newly created Process</returns>
    public Process CreateProcess(Program prog, uint memorySize)
    {
        // Get an array represting the code block
        var processCode = prog.GetMemoryImage();

        // Create a process with a unique id and fixed memory size
        var p = new Process(++ProcessIdPool, memorySize);

        // Map memory to the Process (if available, otherwise freak out)
        MemoryMgr.MapMemoryToProcess(p.ProcessControlBlock.ProcessMemorySize, p.ProcessControlBlock.Pid);

        // Set the initial IP to 0 (that's where exectution will begin)
        p.ProcessControlBlock.InstructionPointer = 0;

        //
        // SETUP CODE SECTION
        //
        // Copy the code in one byte at a time
        uint index = 0;
        foreach (var b in processCode)
        {
            MemoryMgr[p.ProcessControlBlock.Pid, index++] = b;
        }

        //
        // SETUP STACK SECTION
        //
        // Set stack pointer at the end of memory
        //
        p.ProcessControlBlock.StackPointer = memorySize - 1;
        p.ProcessControlBlock.StackSize = uint.Parse(EntryPoint.Configuration["StackSize"]);

        //
        // SETUP CODE SECTION
        //
        // Set the length of the Code section
        //
        var roundedCodeLength = CPU.UtilRoundToBoundary((uint)processCode.Length, CPU.PageSize);
        //uint roundedCodeLength = (uint)(CPU.pageSize * ((processCode.Length / CPU.pageSize) + ((processCode.Length % CPU.pageSize > 0) ? 1: 0)));
        p.ProcessControlBlock.CodeSize = roundedCodeLength;

        //
        // SETUP DATA SECTION
        //
        // Point Global Data just after the Code for now...
        //
        p.ProcessControlBlock.Registers[9] = roundedCodeLength;
        p.ProcessControlBlock.DataSize = uint.Parse(EntryPoint.Configuration["DataSize"]);

        //
        // SETUP HEAP SECTION
        //
        p.ProcessControlBlock.HeapAddrStart = p.ProcessControlBlock.CodeSize + p.ProcessControlBlock.DataSize;
        p.ProcessControlBlock.HeapAddrEnd = p.ProcessControlBlock.ProcessMemorySize - p.ProcessControlBlock.StackSize;


        MemoryMgr.CreateHeapTableForProcess(p);

        // Add ourselves to the RunningProcesses table
        RunningProcesses.Add(p);
        return p;
    }

}

		