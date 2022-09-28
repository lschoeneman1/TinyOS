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
    ///     An event is either Signaled or NonSignaled
    /// </summary>
    public enum EventState
    {
        /// <summary>
        ///     Events are by default NonSignaled
        /// </summary>
        NonSignaled = 0,

        /// <summary>
        ///     Events become Signaled, and Processes that are waiting on them wake up when Signaled
        /// </summary>
        Signaled = 1
    }

    /// <summary>
    ///     Do we output debug for Instructions?
    /// </summary>
    private readonly bool _dumpInstructions;

    /// <summary>
    ///     Contains the <see cref="Process" /> and the <see cref="Process.ProcessControlBlock" /> for all runningProcesses
    /// </summary>
    private readonly Processes _runningProcesses = new Processes();

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
    ///     There are 10 locks, numbered 1 to 10.  Lock 0 is not used.
    ///     We will store 0 when the lock is free, or the ProcessID when the lock is acquired
    /// </summary>
    public uint[] Locks { get; } = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>
    ///     There are 10 events, numbered 1 to 10.  Event 0 is not used
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
    ///     Dumps collected statistics of a process when it's been removed from the <see cref="_runningProcesses" /> table
    /// </summary>
    /// <param name="processIndex">The Index (not the ProcessID!) in the <see cref="_runningProcesses" /> table of a Process</param>
    public void DumpProcessStatistics(int processIndex)
    {
        var process = _runningProcesses[processIndex];

        Console.WriteLine($"Removed Exited Process # {process.ProcessControlBlock.Pid}");
        Console.WriteLine($"  # of Page Faults:      {MemoryMgr.PageFaultsForProcess(process)}");
        Console.WriteLine($"  # of Clock Cycles:     {process.ProcessControlBlock.ClockCycles}");
        Console.WriteLine($"  # of Context Switches: {process.ProcessControlBlock.ContextSwitches}");
    }

    /// <summary>
    ///     The primary control loop for the whole OS.
    ///     Spins through eligible processes and executes their opCodes
    ///     Provides scheduling and removes old processes.
    /// </summary>
    public void Execute()
    {
        while (true)
        {
            //
            // Yank terminated processes
            //
            for (var i = _runningProcesses.Count - 1; i >= 0; i--)
            {
                if (_runningProcesses[i].ProcessControlBlock.State == ProcessState.Terminated)
                {
                    DumpProcessStatistics(i);
                    MemoryMgr.ReleaseMemoryOfProcess(_runningProcesses[i].ProcessControlBlock.Pid);
                    _runningProcesses[i].ProcessControlBlock.HeapPageTable.Clear();
                    ReleaseLocksOfProccess(_runningProcesses[i].ProcessControlBlock.Pid);
                    _runningProcesses.RemoveAt(i);
                    CPU.DumpPhysicalMemory();
                }
            }

            // Sort high priority first + least used clock cycles first to avoid starvation
            // see Process.Compare
            // 
            _runningProcesses.Sort();

            if (_runningProcesses.Count == 0)
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
        foreach (var process in _runningProcesses)
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

        //copy state from PCB to CPU
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
            foreach (var sleepingProcess in _runningProcesses)
            {
                if (sleepingProcess.ProcessControlBlock.State == ProcessState.WaitingAsleep)
                {
                    // a sleepCounter of 0 sleeps forever if we are waiting
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
            //copy state from CPU to PCB
            if (CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingAsleep
                && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnLock
                && CurrentProcess.ProcessControlBlock.State != ProcessState.WaitingOnEvent)
            {
                CurrentProcess.ProcessControlBlock.State = ProcessState.Ready;
            }

            CurrentProcess.ProcessControlBlock.ContextSwitches++;

            DumpContextSwitchOut();

            SaveCPUState();

            //Clear registers for testing
            CPU.registers = new uint[12];
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
    ///     Called on a context switch. Copy the CPU's <see cref="CPU.registers" /> to the <see cref="CurrentProcess" />'s
    ///     <see cref="CPU.registers" />
    /// </summary>
    private void SaveCPUState()
    {
        CPU.registers.CopyTo(CurrentProcess.ProcessControlBlock.Registers, 0);
        CurrentProcess.ProcessControlBlock.ZeroFlag = CPU.ZeroFlag;
        CurrentProcess.ProcessControlBlock.SignFlag = CPU.SignFlag;
        CurrentProcess.ProcessControlBlock.InstructionPointer = CPU.InstructionPointer;
    }

    /// <summary>
    ///     Called on a context switch. Copy the <see cref="CurrentProcess" />'s
    ///     <see cref="Process.ProcessControlBlock" /> to the CPU's <see cref="CPU.registers" />
    /// </summary>
    private void LoadCPUState()
    {
        CurrentProcess.ProcessControlBlock.Registers.CopyTo(CPU.registers, 0);
        CPU.ZeroFlag = CurrentProcess.ProcessControlBlock.ZeroFlag;
        CPU.SignFlag = CurrentProcess.ProcessControlBlock.SignFlag;
        CPU.InstructionPointer = CurrentProcess.ProcessControlBlock.InstructionPointer;
    }

    /// <summary>
    ///     Take as a <see cref="Program" /> and creates a Process object, adding it to the <see cref="_runningProcesses" />
    /// </summary>
    /// <param name="prog">Program to load</param>
    /// <param name="memorySize">Size of memory in bytes to assign to this Process</param>
    /// <returns>The newly created Process</returns>
    public Process createProcess(Program prog, uint memorySize)
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
        var roundedCodeLength = CPU.UtilRoundToBoundary((uint)processCode.Length, CPU.pageSize);
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

        // Add ourselves to the runningProcesses table
        _runningProcesses.Add(p);
        return p;
    }

    /// <summary>
    ///     Releases any locks held by this process.
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


    /// <summary>
    ///     Utility function to fetch a 4 byte unsigned int from Process Memory based on the current <see cref="CPU.InstructionPointer" />
    /// </summary>
    /// <returns>a new uint</returns>
    public uint FetchUIntAndMove()
    {
        var retVal = MemoryMgr.GetUIntFrom(CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer);
        CPU.InstructionPointer += sizeof(uint);
        return retVal;
    }

    /// <summary>
    ///     Increments register
    ///     <pre>1 r1</pre>
    /// </summary>
    public void Incr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Incr == instruction);

        //move to the param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        //increment the register pointed to by this memory
        CPU.registers[register]++;
    }

    /// <summary>
    ///     Adds constant 1 to register 1
    ///     <pre>
    ///         2 r1, $1
    ///     </pre>
    /// </summary>
    public void Addi()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Addi == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} {3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register, param1);
        }

        //increment the register pointed to by this memory by the const next to it
        CPU.registers[register] += param1;
    }

    /// <summary>
    ///     Adds r2 to r1 and stores the value in r1
    ///     <pre>
    ///         3 r1, r2
    ///     </pre>
    /// </summary>
    public void Addr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Addr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} {3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register, param1);
        }

        //add 1st register and 2nd register and put the result in 1st register
        CPU.registers[register] = CPU.registers[register] + CPU.registers[param1];
    }

    /// <summary>
    ///     Compare contents of r1 with 1.  If r1 &lt; 9 set sign flag.  If r1 &gt; 9 clear sign flag.
    ///     If r1 == 9 set zero flag.
    ///     <pre>
    ///         14 r1, $9
    ///     </pre>
    /// </summary>
    public void Cmpi()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Cmpi == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} {3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register, param1);
        }

        //compare register and const
        CPU.ZeroFlag = false;
        if (CPU.registers[register] < param1)
        {
            CPU.SignFlag = true;
        }

        if (CPU.registers[register] > param1)
        {
            CPU.SignFlag = false;
        }

        if (CPU.registers[register] == param1)
        {
            CPU.ZeroFlag = true;
        }
    }

    /// <summary>
    ///     Compare contents of r1 with r2.  If r1 &lt; r2 set sign flag.  If r1 &gt; r2 clear sign flag.
    ///     If r1 == r2 set zero flag.
    ///     <pre>
    ///         15 r1, r2
    ///     </pre>
    /// </summary>
    public void Cmpr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Cmpr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        //compare register and const
        CPU.ZeroFlag = false;
        if (CPU.registers[register1] < CPU.registers[register2])
        {
            CPU.SignFlag = true;
        }

        if (CPU.registers[register1] > CPU.registers[register2])
        {
            CPU.SignFlag = false;
        }

        if (CPU.registers[register1] == CPU.registers[register2])
        {
            CPU.ZeroFlag = true;
        }
    }

    /// <summary>
    ///     Call the procedure at offset r1 bytes from the current instrucion.
    ///     The address of the next instruction to excetute after a return is pushed on the stack
    ///     <pre>
    ///         19 r1
    ///     </pre>
    /// </summary>
    public void Call()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Call == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer);

        CPU.InstructionPointer += CPU.registers[register];
    }

    /// <summary>
    ///     Call the procedure at offset of the bytes in memory pointed by r1 from the current instrucion.
    ///     The address of the next instruction to excetute after a return is pushed on the stack
    ///     <pre>
    ///         20 r1
    ///     </pre>
    /// </summary>
    public void Callm()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Callm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer);

        CPU.InstructionPointer += MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register]];
    }

    /// <summary>
    ///     Pop the return address from the stack and transfer control to this instruction
    ///     <pre>
    ///         21
    ///     </pre>
    /// </summary>
    public void Ret()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Ret == instruction);

        CPU.InstructionPointer++;

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1}", CurrentProcess.ProcessControlBlock.Pid, instruction);
        }

        CPU.InstructionPointer = StackPop(CurrentProcess.ProcessControlBlock.Pid);
    }


    /// <summary>
    ///     Control transfers to the instruction whose address is r1 bytes relative to the current instruction.
    ///     r1 may be negative.
    ///     <pre>
    ///         13 r1
    ///     </pre>
    /// </summary>
    public void Jmp()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jmp == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();


        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        var instructionsToSkip = (int)CPU.registers[register];
        // Do some sillyness to substract if we are a negative number
        if (Math.Sign(instructionsToSkip) == -1)
        {
            CPU.InstructionPointer -= (uint)Math.Abs(instructionsToSkip);
        }
        else
        {
            CPU.InstructionPointer += (uint)instructionsToSkip;
        }
    }

    /// <summary>
    ///     If the sign flag is set, jump to the instruction that is offset r1 bytes from the current instruction
    ///     <pre>
    ///         16 r1
    ///     </pre>
    /// </summary>
    public void Jlt()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jlt == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        if (CPU.SignFlag)
        {
            CPU.InstructionPointer += CPU.registers[register];
        }
    }

    /// <summary>
    ///     If the sign flag is clear, jump to the instruction that is offset r1 bytes from the current instruction
    ///     <pre>
    ///         17 r1
    ///     </pre>
    /// </summary>
    public void Jgt()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jgt == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        if (CPU.SignFlag == false)
        {
            CPU.InstructionPointer += CPU.registers[register];
        }
    }

    /// <summary>
    ///     If the zero flag is set, jump to the instruction that is offset r1 bytes from the current instruction
    ///     <pre>
    ///         18 r1
    ///     </pre>
    /// </summary>
    public void Je()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Je == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        if (CPU.ZeroFlag)
        {
            CPU.InstructionPointer += CPU.registers[register];
        }
    }


    /// <summary>
    ///     Just that, does nothing
    /// </summary>
    public void Noop()
    {
        ;
    }

    /// <summary>
    ///     This opcode causes an exit and the process's memory to be unloaded.
    ///     Another process or the idle process must now be scheduled
    ///     <pre>
    ///         27
    ///     </pre>
    /// </summary>
    public void Exit()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Exit == instruction);

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1}", CurrentProcess.ProcessControlBlock.Pid, instruction);
        }

        CurrentProcess.ProcessControlBlock.State = ProcessState.Terminated;
    }

    /// <summary>
    ///     Moves constant 1 into register 1
    ///     <pre>
    ///         6 r1, $1
    ///     </pre>
    /// </summary>
    public void Movi()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movi == instruction);

        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} {3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register, param2);
        }

        //move VALUE of param into 1st register 
        CPU.registers[register] = param2;
    }

    /// <summary>
    ///     Moves contents of register2 into register 1
    ///     <pre>
    ///         7 r1, r2
    ///     </pre>
    /// </summary>
    public void Movr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        //move VALUE of 2nd register into 1st register 
        CPU.registers[register1] = CPU.registers[register2];
    }

    /// <summary>
    ///     Moves contents of memory pointed to register 2 into register 1
    ///     <pre>
    ///         8 r1, r2
    ///     </pre>
    /// </summary>
    public void Movmr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movmr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        //move VALUE of memory pointed to by 2nd register into 1st register 
        CPU.registers[register1] = MemoryMgr.GetUIntFrom(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register2]);
    }

    /// <summary>
    ///     Moves contents of register 2 into memory pointed to by register 1
    ///     <pre>
    ///         9 r1, r2
    ///     </pre>
    /// </summary>
    public void Movrm()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movrm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        //set memory pointed to by register 1 to contents of register2
        MemoryMgr.SetUIntAt(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register1], CPU.registers[register2]);
    }

    /// <summary>
    ///     Moves contents of memory pointed to by register 2 into memory pointed to by register 1
    ///     <pre>
    ///         10 r1, r2
    ///     </pre>
    /// </summary>
    public void Movmm()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movmm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        //set memory point to by register 1 to contents of memory pointed to by register 2
        MemoryMgr.SetUIntAt(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register1],
            MemoryMgr.GetUIntFrom(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register2]));
    }

    /// <summary>
    ///     Prints out contents of register 1
    ///     <pre>
    ///         11 r1
    ///     </pre>
    /// </summary>
    public void Printr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Printr == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        Console.WriteLine(CPU.registers[register]);
    }

    /// <summary>
    ///     Prints out contents of memory pointed to by register 1
    ///     <pre>
    ///         12 r1
    ///     </pre>
    /// </summary>
    public void Printm()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Printm == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        Console.WriteLine(MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register]]);
    }

    /// <summary>
    ///     Read the next 32-bit value into register r1
    ///     <pre>
    ///         32 r1
    ///     </pre>
    /// </summary>
    public void Input()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Input == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        CPU.registers[register] = uint.Parse(Console.ReadLine());
    }

    /// <summary>
    ///     Sleep the # of clock cycles as indicated in r1.
    ///     Another process or the idle process
    ///     must be scheduled at this point.
    ///     If the time to sleep is 0, the process sleeps infinitely
    ///     <pre>
    ///         25 r1
    ///     </pre>
    /// </summary>
    public void Sleep()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Sleep == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        //Set the number of clockCycles to sleep
        CurrentProcess.ProcessControlBlock.SleepCounter = CPU.registers[register];
        CurrentProcess.ProcessControlBlock.State = ProcessState.WaitingAsleep;
    }

    /// <summary>
    ///     Set the priority of the current process to the value
    ///     in register r1
    ///     <pre>
    ///         26 r1
    ///     </pre>
    /// </summary>
    public void SetPriority()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.SetPriority == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        CurrentProcess.ProcessControlBlock.Priority = (int)Math.Min(CPU.registers[register], (int)ProcessPriority.MaxPriority);
    }

    /// <summary>
    ///     Pushes contents of register 1 onto stack
    ///     <pre>
    ///         4 r1
    ///     </pre>
    /// </summary>
    public void Pushr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Pushr == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register]);
    }

    /// <summary>
    ///     Pushes constant 1 onto stack
    ///     <pre>
    ///         5 $1
    ///     </pre>
    /// </summary>
    public void Pushi()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Pushi == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var param = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} {2}", CurrentProcess.ProcessControlBlock.Pid, instruction, param);
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, param);
    }

    /// <summary>
    ///     Terminate the process whose id is in the register r1
    ///     <pre>
    ///         34 r1
    ///     </pre>
    /// </summary>
    public void TerminateProcess()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.TerminateProcess == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        foreach (var p in _runningProcesses)
        {
            if (p.ProcessControlBlock.Pid == CPU.registers[register])
            {
                p.ProcessControlBlock.State = ProcessState.Terminated;
                Console.WriteLine("Process {0} has forceably terminated Process {1}", CurrentProcess.ProcessControlBlock.Pid,
                    p.ProcessControlBlock.Pid);
                break;
            }
        }
    }

    /// <summary>
    ///     Pop the contents at the top of the stack into register r1
    ///     <pre>
    ///         35 r1
    ///     </pre>
    /// </summary>
    public void Popr()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Popr == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        CPU.registers[register] = StackPop(CurrentProcess.ProcessControlBlock.Pid);
    }

    /// <summary>
    ///     set the bytes starting at address r1 of length r2 to zero
    ///     <pre>
    ///         33 r1, r2
    ///     </pre>
    /// </summary>
    public void MemoryClear()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.MemoryClear == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        //move VALUE of memory pointed to by 2nd register into 1st register 
        MemoryMgr.SetMemoryOfProcess(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register1], CPU.registers[register2], 0);
    }

    /// <summary>
    ///     Pop the contents at the top of the stack into the memory pointed to by register r1
    ///     <pre>
    ///         36 r1
    ///     </pre>
    /// </summary>
    public void Popm()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Popm == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        MemoryMgr.SetUIntAt(CurrentProcess.ProcessControlBlock.Pid, CPU.registers[register], StackPop(CurrentProcess.ProcessControlBlock.Pid));
    }

    /// <summary>
    ///     Acquire the OS lock whose # is provided in register r1.
    ///     Icf the lock is not held by the current process
    ///     the operation is a no-op
    ///     <pre>
    ///         23 r1
    ///     </pre>
    /// </summary>
    public void AcquireLock()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.AcquireLock == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        //Are we the first ones here? with a valid lock?
        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            if (Locks[CPU.registers[register]] == 0)
            {
                //Set the lock specified in the register as locked...
                Locks[CPU.registers[register]] = CurrentProcess.ProcessControlBlock.Pid;
            }
            else if (Locks[CPU.registers[register]] == CurrentProcess.ProcessControlBlock.Pid)
            {
                //No-Op, we already have this lock
                ;
            }
            else
            {
                //Get in line for this lock
                CurrentProcess.ProcessControlBlock.WaitingLock = CPU.registers[register];
                CurrentProcess.ProcessControlBlock.State = ProcessState.WaitingOnLock;
            }
        }
    }

    /// <summary>
    ///     Release the OS lock whose # is provided in register r1.
    ///     Another process or the idle process
    ///     must be scheduled at this point.
    ///     if the lock is not held by the current process,
    ///     the instruction is a no-op
    ///     <pre>
    ///         24 r1
    ///     </pre>
    /// </summary>
    public void ReleaseLock()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.ReleaseLock == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        //Release only if we already have this lock, and it's a valid lock
        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            if (Locks[CPU.registers[register]] == CurrentProcess.ProcessControlBlock.Pid)
            {
                //set the lock back to 0 (the OS)
                Locks[CPU.registers[register]] = 0;
            }
        }
    }

    /// <summary>
    ///     Signal the event indicated by the value in register r1
    ///     <pre>
    ///         30 r1
    ///     </pre>
    /// </summary>
    public void SignalEvent()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.SignalEvent == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            Events[CPU.registers[register]] = EventState.Signaled;
        }
    }

    /// <summary>
    ///     Wait for the event in register r1 to be triggered resulting in a context-switch
    ///     <pre>
    ///         31 r1
    ///     </pre>
    /// </summary>
    public void WaitEvent()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.WaitEvent == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register);
        }

        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            CurrentProcess.ProcessControlBlock.WaitingEvent = CPU.registers[register];
            CurrentProcess.ProcessControlBlock.State = ProcessState.WaitingOnEvent;
        }
    }

    /// <summary>
    ///     Map the shared memory region identified by r1 and return the start address in r2
    ///     <pre>
    ///         29 r1, r2
    ///     </pre>
    /// </summary>
    public void MapSharedMem()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.MapSharedMem == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        CPU.registers[register2] = MemoryMgr.MapSharedMemoryToProcess(CPU.registers[register1], CurrentProcess.ProcessControlBlock.Pid);
    }


    /// <summary>
    ///     Allocate memory of the size equal to r1 bytes and return the address of the new memory in r2.
    ///     If failed, r2 is cleared to 0.
    ///     <pre>
    ///         22 r1, r2
    ///     </pre>
    /// </summary>
    public void Alloc()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Alloc == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove(); //bytes requested
        var register2 = FetchUIntAndMove(); //address returned

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2} r{3}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1, register2);
        }

        var addr = MemoryMgr.ProcessHeapAlloc(CurrentProcess, CPU.registers[register1]);

        CPU.registers[register2] = addr;
    }

    /// <summary>
    ///     Free the memory allocated whose address is in r1
    ///     <pre>
    ///         28 r1
    ///     </pre>
    /// </summary>
    public void FreeMemory()
    {
        //get the instruction and make sure we should be here
        var instruction = (InstructionType)MemoryMgr[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.FreeMemory == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove(); //address of memory

        if (_dumpInstructions)
        {
            Console.WriteLine(" Pid:{0} {1} r{2}", CurrentProcess.ProcessControlBlock.Pid, instruction, register1);
        }

        MemoryMgr.ProcessHeapFree(CurrentProcess, CPU.registers[register1]);
    }


    /// <summary>
    ///     Push a uint on the stack for this Process
    /// </summary>
    /// <param name="processid">The Process Id</param>
    /// <param name="avalue">The uint for the stack</param>
    public void StackPush(uint processid, uint avalue)
    {
        CPU.StackPointer -= 4;

        //Are we blowing the stack?
        if (CPU.StackPointer < CurrentProcess.ProcessControlBlock.ProcessMemorySize - 1 - CurrentProcess.ProcessControlBlock.StackSize)
        {
            throw new StackException(CurrentProcess.ProcessControlBlock.Pid,
                CurrentProcess.ProcessControlBlock.ProcessMemorySize - 1 - CurrentProcess.ProcessControlBlock.StackSize - CPU.StackPointer);
        }

        MemoryMgr.SetUIntAt(processid, CPU.StackPointer, avalue);
    }

    /// <summary>
    ///     Pop a uint off the stack for this Process
    /// </summary>
    /// <param name="processid">The Process ID</param>
    /// <returns>the uint from the stack</returns>
    public uint StackPop(uint processid)
    {
        var retVal = MemoryMgr.GetUIntFrom(processid, CPU.StackPointer);
        MemoryMgr.SetMemoryOfProcess(processid, CPU.StackPointer, 4, 0);
        CPU.StackPointer += 4;
        return retVal;
    }
}