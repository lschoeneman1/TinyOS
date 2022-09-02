using System;
using System.Diagnostics;

namespace TinyOSCore;

/// <summary>
///     The delegate (object-oriented function pointer) definition for an OS System Call.
///     ALl opCodes will be mapped to a function that matches this signature
/// </summary>
public delegate void SystemCall();

/// <summary>
///     The definition of an Operarting System, including a <see cref="MemoryManager" /> and a
///     <see cref="ProcessCollection" />
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
    ///     Holds a reference to the current running <see cref="Process" />
    /// </summary>
    public Process currentProcess;

    /// <summary>
    ///     There are 10 events, numbered 1 to 10.  Event 0 is not used
    /// </summary>
    public EventState[] events = new EventState[11];

    /// <summary>
    ///     There are 10 locks, numbered 1 to 10.  Lock 0 is not used.
    ///     We will store 0 when the lock is free, or the ProcessID when the lock is acquired
    /// </summary>
    public uint[] locks = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>
    ///     A reference to the <see cref="MemoryManager" /> Class.  A <see cref="Process" /> memory accesses go
    ///     through this class.
    /// </summary>
    /// <example>
    ///     theOS.memoryMgr[processId, 5]; //accesses memory at address 5
    /// </example>
    public MemoryManager memoryMgr;

    /// <summary>
    ///     Contains the <see cref="Process" /> and the <see cref="Process.ProcessControlBlock" /> for all runningProcesses
    /// </summary>
    private readonly ProcessCollection _runningProcesses = new ProcessCollection();

    /// <summary>
    ///     Public constructor for the OS
    /// </summary>
    /// <param name="virtualMemoryBytes">The number of "addressable" bytes of memory for the whole OS.</param>
    public OS(uint virtualMemoryBytes)
    {
        memoryMgr = new MemoryManager(virtualMemoryBytes);
        _dumpInstructions = bool.Parse(EntryPoint.Configuration["DumpInstruction"]);
    }

    /// <summary>
    ///     This counter is incremented as new processes are created.
    ///     It provides a unique id for a process. Process Id 0 is assumed to be the OS.
    /// </summary>
    public static uint ProcessIdPool { get; set; }

    /// <summary>
    ///     Checks if the <see cref="currentProcess" /> is eligible to run
    /// </summary>
    /// <returns>true if the <see cref="currentProcess" /> is eligible to run</returns>
    public bool CurrentProcessIsEligible()
    {
        if (currentProcess == null) return false;

        if (currentProcess.PCB.State == ProcessState.Terminated
            || currentProcess.PCB.State == ProcessState.WaitingOnLock
            || currentProcess.PCB.State == ProcessState.WaitingAsleep
            || currentProcess.PCB.State == ProcessState.WaitingOnEvent)
            return false;
        return true;
    }

    /// <summary>
    ///     Dumps collected statistics of a process when it's been removed from the <see cref="_runningProcesses" /> table
    /// </summary>
    /// <param name="processIndex">The Index (not the ProcessID!) in the <see cref="_runningProcesses" /> table of a Process</param>
    public void DumpProcessStatistics(int processIndex)
    {
        var p = _runningProcesses[processIndex];

        Console.WriteLine("Removed Exited Process # {0}", p.PCB.Pid);
        Console.WriteLine("  # of Page Faults:      {0}", memoryMgr.PageFaultsForProcess(p));
        Console.WriteLine("  # of Clock Cycles:     {0}", p.PCB.ClockCycles);
        Console.WriteLine("  # of Context Switches: {0}", p.PCB.ContextSwitches);
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
                if (_runningProcesses[i].PCB.State == ProcessState.Terminated)
                {
                    DumpProcessStatistics(i);
                    memoryMgr.ReleaseMemoryOfProcess(_runningProcesses[i].PCB.Pid);
                    _runningProcesses[i].PCB.HeapPageTable.Clear();
                    ReleaseLocksOfProccess(_runningProcesses[i].PCB.Pid);
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
                if (bool.Parse(EntryPoint.Configuration["PauseOnExit"])) Console.ReadLine();
                Environment.Exit(0);
            }
            else
            {
                foreach (var p in _runningProcesses)
                {
                    switch (p.PCB.State)
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
                            currentProcess = p;

                            //copy state from PCB to CPU
                            LoadCPUState();

                            DumpContextSwitchIn();

                            // Reset this flag. If we need to interrupt execution 
                            // because a lock has been made available
                            // or an Event has signaled, we can preempt the current process
                            var preemptCurrentProcess = false;

                            while (CurrentProcessIsEligible())
                            {
                                currentProcess.PCB.State = ProcessState.Running;

                                //CPU.DumpPhysicalMemory();
                                //CPU.DumpRegisters();

                                try
                                {
                                    CPU.ExecuteNextOpCode();
                                    currentProcess.PCB.ClockCycles++;
                                }
                                catch (MemoryException e)
                                {
                                    Console.WriteLine(e.ToString());
                                    CPU.DumpRegisters();
                                    currentProcess.PCB.State = ProcessState.Terminated;
                                }
                                catch (StackException e)
                                {
                                    Console.WriteLine(e.ToString());
                                    CPU.DumpRegisters();
                                    currentProcess.PCB.State = ProcessState.Terminated;
                                }
                                catch (HeapException e)
                                {
                                    Console.WriteLine(e.ToString());
                                    CPU.DumpRegisters();
                                    currentProcess.PCB.State = ProcessState.Terminated;
                                }

                                CPU.DumpPhysicalMemory();
                                CPU.DumpRegisters();

                                //
                                // Update any sleeping processes
                                //
                                foreach (var sleepingProcess in _runningProcesses)
                                {
                                    switch (sleepingProcess.PCB.State)
                                    {
                                        case ProcessState.WaitingAsleep:
                                            // a sleepCounter of 0 sleeps forever if we are waiting
                                            if (sleepingProcess.PCB.SleepCounter != 0)
                                                //If we JUST reached 0, wake up!
                                                if (--sleepingProcess.PCB.SleepCounter == 0)
                                                {
                                                    sleepingProcess.PCB.State = ProcessState.Ready;
                                                    preemptCurrentProcess = true;
                                                }

                                            break;
                                        case ProcessState.WaitingOnEvent:
                                            // Are we waiting for an event?  We'd better be!
                                            Debug.Assert(sleepingProcess.PCB.WaitingEvent != 0);

                                            // Had the event been signalled recently?
                                            if (events[sleepingProcess.PCB.WaitingEvent] == EventState.Signaled)
                                            {
                                                events[sleepingProcess.PCB.WaitingEvent] = EventState.NonSignaled;
                                                sleepingProcess.PCB.State = ProcessState.Ready;
                                                sleepingProcess.PCB.WaitingEvent = 0;
                                                preemptCurrentProcess = true;
                                            }

                                            break;
                                        case ProcessState.WaitingOnLock:
                                            // We are are in the WaitingOnLock state, we can't wait on the "0" lock
                                            Debug.Assert(sleepingProcess.PCB.WaitingLock != 0);

                                            // Has the lock be released recently?
                                            if (locks[sleepingProcess.PCB.WaitingLock] == 0)
                                            {
                                                // Acquire the Lock and wake up!
                                                locks[sleepingProcess.PCB.WaitingLock] = sleepingProcess.PCB.WaitingLock;
                                                sleepingProcess.PCB.State = ProcessState.Ready;
                                                preemptCurrentProcess = true;
                                                sleepingProcess.PCB.WaitingLock = 0;
                                                
                                            }

                                            break;
                                    }
                                }

                                // Have we used up our slice of time?
                                var eligible = currentProcess.PCB.ClockCycles == 0 
                                                    ||  currentProcess.PCB.ClockCycles % currentProcess.PCB.TimeQuantum != 0;
                                if (!eligible)
                                {
                                    break;
                                }

                                if (preemptCurrentProcess)
                                {
                                    break;
                                }
                            }

                            if (currentProcess.PCB.State != ProcessState.Terminated)
                            {
                                //copy state from CPU to PCB
                                if (currentProcess.PCB.State != ProcessState.WaitingAsleep
                                    && currentProcess.PCB.State != ProcessState.WaitingOnLock
                                    && currentProcess.PCB.State != ProcessState.WaitingOnEvent)
                                    currentProcess.PCB.State = ProcessState.Ready;
                                {
                                    currentProcess.PCB.ContextSwitches++;
                                }

                                DumpContextSwitchOut();

                                SaveCPUState();

                                //Clear registers for testing
                                CPU.registers = new uint[12];
                            }

                            currentProcess = null;
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     If the DumpContextSwitch Configuration option is set to True, reports the Context Switch.
    ///     Used for debugging
    /// </summary>
    public void DumpContextSwitchIn()
    {
        if (!bool.Parse(EntryPoint.Configuration["DumpContextSwitch"]))
        {
            return;
        }
        Console.WriteLine($"Switching in Process {currentProcess.PCB.Pid} with ip at {currentProcess.PCB.InstructionPointer}");
    }

    /// <summary>
    ///     If the DumpContextSwitch Configuration option is set to True, reports the Context Switch.
    ///     Used for debugging
    /// </summary>
    public void DumpContextSwitchOut()
    {
        if (!bool.Parse(EntryPoint.Configuration["DumpContextSwitch"]))
        {
            return;
        }
        Console.WriteLine($"Switching out Process {currentProcess.PCB.Pid} with ip at {CPU.InstructionPointer}");
    }

    /// <summary>
    ///     Outputs a view of memory from the Process's point of view
    /// </summary>
    /// <param name="p">The Process to Dump</param>
    public void DumpProcessMemory(Process p)
    {
        var address = 0;
        for (uint i = 0; i < p.PCB.ProcessMemorySize; i++)
        {
            var b = memoryMgr[p.PCB.Pid, i];
            if (address == 0 || address % 16 == 0)
            {
                Console.Write($"{Environment.NewLine}{address,-4:000} ");
            }
            address++;
            if (b == 0)
            {
                Console.Write($"{"-",3}");
            }
            else
            {
                Console.Write($"{(int)b,3}");
            }

            if (address % 4 == 0 && address % 16 != 0)
            {
                Console.Write("  :");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    ///     Called on a context switch. Copy the CPU's <see cref="CPU.registers" /> to the <see cref="currentProcess" />'s
    ///     <see cref="CPU.registers" />
    /// </summary>
    private void SaveCPUState()
    {
        CPU.registers.CopyTo(currentProcess.PCB.Registers, 0);
        currentProcess.PCB.ZeroFlag = CPU.ZeroFlag;
        currentProcess.PCB.SignFlag = CPU.SignFlag;
        currentProcess.PCB.InstructionPointer = CPU.InstructionPointer;
    }

    /// <summary>
    ///     Called on a context switch. Copy the <see cref="currentProcess" />'s
    ///     <see cref="Process.ProcessControlBlock.Registers" /> to the CPU's <see cref="CPU.registers" />
    /// </summary>
    private void LoadCPUState()
    {
        currentProcess.PCB.Registers.CopyTo(CPU.registers, 0);
        CPU.ZeroFlag = currentProcess.PCB.ZeroFlag;
        CPU.SignFlag = currentProcess.PCB.SignFlag;
        CPU.InstructionPointer = currentProcess.PCB.InstructionPointer;
    }

    /// <summary>
    ///     Take as a <see cref="Program" /> and creates a Process object, adding it to the <see cref="_runningProcesses" />
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
        memoryMgr.MapMemoryToProcess(p.PCB.ProcessMemorySize, p.PCB.Pid);

        // Set the initial IP to 0 (that's where exectution will begin)
        p.PCB.InstructionPointer = 0;

        //
        // SETUP CODE SECTION
        //
        // Copy the code in one byte at a time
        uint index = 0;
        foreach (var b in processCode)
        {
            memoryMgr[p.PCB.Pid, index++] = b;
        }

        //
        // SETUP STACK SECTION
        //
        // Set stack pointer at the end of memory
        //
        p.PCB.StackPointer = memorySize - 1;
        p.PCB.StackSize = uint.Parse(EntryPoint.Configuration["StackSize"]);

        //
        // SETUP CODE SECTION
        //
        // Set the length of the Code section
        //
        var roundedCodeLength = CPU.UtilRoundToBoundary((uint)processCode.Length, CPU.pageSize);
        //uint roundedCodeLength = (uint)(CPU.pageSize * ((processCode.Length / CPU.pageSize) + ((processCode.Length % CPU.pageSize > 0) ? 1: 0)));
        p.PCB.CodeSize = roundedCodeLength;

        //
        // SETUP DATA SECTION
        //
        // Point Global Data just after the Code for now...
        //
        p.PCB.Registers[9] = roundedCodeLength;
        p.PCB.DataSize = uint.Parse(EntryPoint.Configuration["DataSize"]);

        //
        // SETUP HEAP SECTION
        //
        p.PCB.HeapAddrStart = p.PCB.CodeSize + p.PCB.DataSize;
        p.PCB.HeapAddrEnd = p.PCB.ProcessMemorySize - p.PCB.StackSize;


        memoryMgr.CreateHeapTableForProcess(p);

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
        for (var i = 0; i < locks.Length; i++)
        {
            if (locks[i] == pid)
            {
                locks[i] = 0;
            }
        }
    }


    /// <summary>
    ///     Utility function to fetch a 4 byte unsigned int from Process Memory based on the current
    ///     <see cref="CPU.InstructionPointer" />
    /// </summary>
    /// <returns>a new uint</returns>
    public uint FetchUIntAndMove()
    {
        var retVal = memoryMgr.GetUIntFrom(currentProcess.PCB.Pid, CPU.InstructionPointer);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Incr == instruction);

        //move to the param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Addi == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register} {param1}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Addr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register} {param1}");
        }

        //add 1st register and 2nd register and put the result in 1st register
        CPU.registers[register] += CPU.registers[param1];
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Cmpi == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register} {param1}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Cmpr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Call == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        StackPush(currentProcess.PCB.Pid, CPU.InstructionPointer);

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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Callm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        StackPush(currentProcess.PCB.Pid, CPU.InstructionPointer);

        CPU.InstructionPointer += memoryMgr[currentProcess.PCB.Pid, CPU.registers[register]];
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Ret == instruction);

        CPU.InstructionPointer++;

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction}");
        }

        CPU.InstructionPointer = StackPop(currentProcess.PCB.Pid);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jmp == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();


        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jlt == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        if (CPU.SignFlag) CPU.InstructionPointer += CPU.registers[register];
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jgt == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        if (CPU.SignFlag == false) CPU.InstructionPointer += CPU.registers[register];
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Je == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Exit == instruction);

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction}");
        }

        currentProcess.PCB.State = ProcessState.Terminated;
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movi == instruction);

        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register} {param2}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movmr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
        }

        //move VALUE of memory pointed to by 2nd register into 1st register 
        CPU.registers[register1] = memoryMgr.GetUIntFrom(currentProcess.PCB.Pid, CPU.registers[register2]);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movrm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
        }

        //set memory pointed to by register 1 to contents of register2
        memoryMgr.SetUIntAt(currentProcess.PCB.Pid, CPU.registers[register1], CPU.registers[register2]);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movmm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
        }

        //set memory point to by register 1 to contents of memory pointed to by register 2
        memoryMgr.SetUIntAt(currentProcess.PCB.Pid, 
                            CPU.registers[register1],
                            memoryMgr.GetUIntFrom(currentProcess.PCB.Pid, CPU.registers[register2]));
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Printr == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Printm == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        Console.WriteLine(memoryMgr[currentProcess.PCB.Pid, CPU.registers[register]]);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Input == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        CPU.registers[register] = uint.Parse(Console.ReadLine() ?? string.Empty);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Sleep == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        //Set the number of clockCycles to sleep
        currentProcess.PCB.SleepCounter = CPU.registers[register];
        currentProcess.PCB.State = ProcessState.WaitingAsleep;
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.SetPriority == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        currentProcess.PCB.Priority = (int)Math.Min(CPU.registers[register], (int)ProcessPriority.MaxPriority);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Pushr == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        StackPush(currentProcess.PCB.Pid, CPU.registers[register]);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Pushi == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var param = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} {param}");
        }

        StackPush(currentProcess.PCB.Pid, param);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.TerminateProcess == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions) Console.WriteLine(" Pid:{0} {1} r{2}", currentProcess.PCB.Pid, instruction, register);

        foreach (var p in _runningProcesses)
        {
            if (p.PCB.Pid == CPU.registers[register])
            {
                p.PCB.State = ProcessState.Terminated;
                Console.WriteLine($"Process {currentProcess.PCB.Pid} has forceably terminated Process {p.PCB.Pid}");
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Popr == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        CPU.registers[register] = StackPop(currentProcess.PCB.Pid);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.MemoryClear == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
        }

        //move VALUE of memory pointed to by 2nd register into 1st register 
        memoryMgr.SetMemoryOfProcess(currentProcess.PCB.Pid, CPU.registers[register1], CPU.registers[register2], 0);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Popm == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        memoryMgr.SetUIntAt(currentProcess.PCB.Pid, CPU.registers[register], StackPop(currentProcess.PCB.Pid));
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.AcquireLock == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        //Are we the first ones here? with a valid lock?
        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            if (locks[CPU.registers[register]] == 0)
            {
                //Set the lock specified in the register as locked...
                locks[CPU.registers[register]] = currentProcess.PCB.Pid;
            }
            else if (locks[CPU.registers[register]] == currentProcess.PCB.Pid)
            {
                //No-Op, we already have this lock
                
            }
            else
            {
                //Get in line for this lock
                currentProcess.PCB.WaitingLock = CPU.registers[register];
                currentProcess.PCB.State = ProcessState.WaitingOnLock;
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.ReleaseLock == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        //Release only if we already have this lock, and it's a valid lock
        if (CPU.registers[register] > 0 
            && CPU.registers[register] <= 10 
            && locks[CPU.registers[register]] == currentProcess.PCB.Pid)
        {
            //set the lock back to 0 (the OS)
            locks[CPU.registers[register]] = 0;
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.SignalEvent == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            events[CPU.registers[register]] = EventState.Signaled;
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.WaitEvent == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register}");
        }

        if (CPU.registers[register] > 0 && CPU.registers[register] <= 10)
        {
            currentProcess.PCB.WaitingEvent = CPU.registers[register];
            currentProcess.PCB.State = ProcessState.WaitingOnEvent;
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.MapSharedMem == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
        }

        CPU.registers[register2] = memoryMgr.MapSharedMemoryToProcess(CPU.registers[register1], currentProcess.PCB.Pid);
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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Alloc == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove(); //bytes requested
        var register2 = FetchUIntAndMove(); //address returned

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{currentProcess.PCB.Pid} {instruction} r{register1} r{register2}");
        }

        var addr = memoryMgr.ProcessHeapAlloc(currentProcess, CPU.registers[register1]);

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
        var instruction = (InstructionType)memoryMgr[currentProcess.PCB.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.FreeMemory == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove(); //address of memory

        if (_dumpInstructions) {Console.WriteLine(" Pid:{0} {1} r{2}", currentProcess.PCB.Pid, instruction, register1);}

        memoryMgr.ProcessHeapFree(currentProcess, CPU.registers[register1]);
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
        if (CPU.StackPointer < currentProcess.PCB.ProcessMemorySize - 1 - currentProcess.PCB.StackSize)
        {
            throw new StackException(currentProcess.PCB.Pid, currentProcess.PCB.ProcessMemorySize - 1 - currentProcess.PCB.StackSize - CPU.StackPointer);

        }

        memoryMgr.SetUIntAt(processid, CPU.StackPointer, avalue);
    }

    /// <summary>
    ///     Pop a uint off the stack for this Process
    /// </summary>
    /// <param name="processid">The Process ID</param>
    /// <returns>the uint from the stack</returns>
    public uint StackPop(uint processid)
    {
        var retVal = memoryMgr.GetUIntFrom(processid, CPU.StackPointer);
        memoryMgr.SetMemoryOfProcess(processid, CPU.StackPointer, 4, 0);
        CPU.StackPointer += 4;
        return retVal;
    }
}