using System;
using System.Collections;
using System.Diagnostics;
using TinyOSCore.Core;
using TinyOSCore.Exceptions;
using TinyOSCore.MemoryManagement;
using Process = TinyOSCore.Core.Process;

namespace TinyOSCore.Cpu;

/// <summary>
///     CPU is never instanciated, but is "always" there...like a real CPU. :)  It holds <see cref="PhysicalMemory" />
///     and the <see cref="Registers" />.  It also provides a mapping from <see cref="Instruction" />s to SystemCalls in
///     the <see cref="OS" />.
/// </summary>
public abstract class CPU
{
    /// <summary>
    ///     The size of a memory page for this system.  This should be a multiple of 4.  Small sizes (like 4) will
    ///     cause the system to thrash and page often.  16 is a nice compromise for such a small system.
    ///     64 might also work well.  This probably won't change, but it is nice to be able to.
    ///     This is loaded from Configuration on a call to <see cref="InitPhysicalMemory" />
    /// </summary>
    public static uint PageSize { get; set; }
    
    /// <summary>
    /// CPU instruction implementations.  Done to remove dependency on OS
    /// </summary>
    private static readonly SystemCalls InstructionImplementation = new SystemCalls();

    /// <summary>
    ///     The clock for the system.  This increments as we execute each <see cref="Instruction" />.
    /// </summary>
    public static uint Clock { get; set; }

    /// <summary>
    ///     The CPU's reference to the <see cref="OS" />.  This is set by the <see cref="EntryPoint" />.
    /// </summary>
    public static OS TheOS { get; set; } = null;

    /// <summary>
    ///     Here is the actual array of bytes that contains the physical memory for this CPU.
    /// </summary>
    internal static byte[] PhysicalMemory { get; set; }

    /// <summary>
    ///     We have 10 registers.  R11 is the <see cref="InstructionPointer" />, and we don't use R0.  R10 is the
    ///     <see cref="StackPointer" />.  So,
    ///     that's 1 to 10, and 11.
    /// </summary>
    internal static uint[] Registers { get; set; } = new uint[12];

    /// <summary>
    ///     We have a Sign Flag and a Zero Flag in a <see cref="BitArray" />
    /// </summary>
    private static readonly BitArray BitFlagRegisters = new BitArray(2, false);

    /// <summary>
    ///     Initialized our <see cref="PhysicalMemory" /> array that represents physical memory.  Should only be called once.
    /// </summary>
    /// <param name="memorySize">The size of physical memory</param>
    public static void InitPhysicalMemory(uint memorySize)
    {
        PageSize = uint.Parse(EntryPoint.Configuration["MemoryPageSize"]);

        var newMemorySize = UtilRoundToBoundary(memorySize, PageSize);

        // Initalize Physical Memory
        PhysicalMemory = new byte[newMemorySize];

        if (newMemorySize != memorySize)
        {
            Console.WriteLine($"CPU: Memory was expanded from {memorySize} bytes to {newMemorySize} bytes to a page boundary.{Environment.NewLine}");
        }
    }


    /// <summary>
    ///     Takes the process id from the <see cref="OS.CurrentProcess" /> and the CPU's <see cref="InstructionPointer" /> and
    ///     gets the next <see cref="Instruction" /> from memory.  The <see cref="InstructionType" /> translates
    ///     via an array of <see cref="SystemCall" />s and retrives a <see cref="Delegate" /> from
    ///     <see cref="OpCodeToSysCall" />
    ///     and calls it.
    /// </summary>
    public static void ExecuteNextOpCode()
    {
        // Set up transfer info needed by CPU
        InstructionImplementation.CurrentProcess = TheOS.CurrentProcess;
        InstructionImplementation.Events = TheOS.Events;
        InstructionImplementation.Locks = TheOS.Locks;
        InstructionImplementation.MemoryManager = TheOS.MemoryMgr;
        InstructionImplementation.RunningProcesses = TheOS.RunningProcesses;
        // The opCode still is pointed to by CPU.ip, but the memory access is protected
        OpCodeToSysCall((InstructionType)TheOS.MemoryMgr[TheOS.CurrentProcess.ProcessControlBlock.Pid, InstructionPointer]);
        Clock++;
    }

    /// <summary>
    ///     The <see cref="InstructionType" /> translates via an array of <see cref="SystemCall" />s and
    ///     retrives a <see cref="Delegate" /> and calls it.
    /// </summary>
    /// <param name="opCode">An <see cref="InstructionType" /> enum that maps to a <see cref="SystemCall" /></param>
    public static void OpCodeToSysCall(InstructionType opCode)
    {
        #region System Calls Map

        SystemCall[] systemCalls =
        {
            InstructionImplementation.Noop, //0

            InstructionImplementation.Incr, //1
            InstructionImplementation.Addi, //2
            InstructionImplementation.Addr, //3
            InstructionImplementation.Pushr, //4
            InstructionImplementation.Pushi, //5

            InstructionImplementation.Movi, //6
            InstructionImplementation.Movr, //7
            InstructionImplementation.Movmr, //8
            InstructionImplementation.Movrm, //9
            InstructionImplementation.Movmm, //10

            InstructionImplementation.Printr, //11
            InstructionImplementation.Printm, //12
            InstructionImplementation.Jmp, //13
            InstructionImplementation.Cmpi, //14
            InstructionImplementation.Cmpr, //15

            InstructionImplementation.Jlt, //16
            InstructionImplementation.Jgt, //17
            InstructionImplementation.Je, //18
            InstructionImplementation.Call, //19
            InstructionImplementation.Callm, //20

            InstructionImplementation.Ret, //21
            InstructionImplementation.Alloc, //22
            InstructionImplementation.AcquireLock, //23
            InstructionImplementation.ReleaseLock, //24
            InstructionImplementation.Sleep, //25

            InstructionImplementation.SetPriority, //26
            InstructionImplementation.Exit, //27
            InstructionImplementation.FreeMemory, //28
            InstructionImplementation.MapSharedMem, //29
            InstructionImplementation.SignalEvent, //30

            InstructionImplementation.WaitEvent, //31
            InstructionImplementation.Input, //32
            InstructionImplementation.MemoryClear, //33
            InstructionImplementation.TerminateProcess, //34
            InstructionImplementation.Popr, //35

            InstructionImplementation.Popm //36
        };
        

        #endregion

        Debug.Assert(opCode is >= InstructionType.Incr and <= InstructionType.Popm);

        var call = systemCalls[(int)opCode];
        call();
    }

    #region Public Accessors

    /// <summary>
    ///     Public get/set accessor for the Sign Flag
    /// </summary>
    public static bool SignFlag
    {
        get => BitFlagRegisters[0];
        set => BitFlagRegisters[0] = value;
    }

    /// <summary>
    ///     Public get/set accessor for the Zero Flag
    /// </summary>
    public static bool ZeroFlag
    {
        get => BitFlagRegisters[1];
        set => BitFlagRegisters[1] = value;
    }

    /// <summary>
    ///     Public get/set accessor for Stack Pointer
    /// </summary>
    public static uint StackPointer
    {
        get => Registers[10];
        set => Registers[10] = value;
    }

    /// <summary>
    ///     Public get/set access for the CPU's Instruction Pointer
    /// </summary>
    public static uint InstructionPointer
    {
        get => Registers[11];
        set => Registers[11] = value;
    }

    #endregion


    #region Dump Functions for debugging

    /// <summary>
    ///     Dumps the values of <see cref="Registers" /> as the <see cref="CPU" /> currently sees it.
    /// </summary>
    public static void DumpRegisters()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpRegisters"]) == false)
        {
            return;
        }

        Console.WriteLine($"CPU Registers: r1 {Registers[1],-8:G}          r6  {Registers[6],-8:G}");
        Console.WriteLine($"               r2 {Registers[2],-8:G}          r7  {Registers[7],-8:G}");
        Console.WriteLine($"               r3 {Registers[3],-8:G}    (pid) r8  {Registers[8],-8:G}");
        Console.WriteLine($"               r4 {Registers[4],-8:G}   (data) r9 . {Registers[9],-8:G}");
        Console.WriteLine($"               r5 {Registers[5],-8:G}     (sp) r10 {Registers[10]}");
        Console.WriteLine($"               sf {SignFlag,-8:G}          ip  {InstructionPointer}");
        Console.WriteLine($"               zf {ZeroFlag,-8:G}      ");
    }

    /// <summary>
    ///     Dumps the current <see cref="Instruction" /> for the current process at the current
    ///     <see cref="InstructionPointer" />
    /// </summary>
    public static void DumpInstruction()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpInstruction"]) == false)
        {
            return;
        }

        Console.WriteLine(
            $" Pid:{Registers[8]} {(InstructionType)TheOS.MemoryMgr[TheOS.CurrentProcess.ProcessControlBlock.Pid, InstructionPointer]} {(uint)TheOS.MemoryMgr[TheOS.CurrentProcess.ProcessControlBlock.Pid, InstructionPointer]}");
    }

    /// <summary>
    ///     Dumps the content of the CPU's <see cref="PhysicalMemory" /> array.
    /// </summary>
    public static void DumpPhysicalMemory()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpPhysicalMemory"]) == false)
        {
            return;
        }

        var address = 0;
        foreach (var b in PhysicalMemory)
        {
            if (address == 0 || address % 16 == 0)
            {
                Console.Write(Environment.NewLine + "{0,-4:000} ", address);
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

    #endregion

    #region Type Conversion and Utility Functions

    /// <summary>
    ///     Pins down a section of memory and converts an array of bytes into an unsigned int (<see cref="uint" />)
    /// </summary>
    /// <param name="bytesIn">array of bytes to convert</param>
    /// <returns>value of bytes as a uint</returns>
    public static unsafe uint BytesToUInt(byte[] bytesIn)
    {
        fixed (byte* otherbytes = bytesIn)
        {
            var ut = (uint*)&otherbytes[0];
            var newUint = *ut;
            return newUint;
        }
    }

    /// <summary>
    ///     Pins down a section of memory and converts an unsigned int into an array of (<see cref="byte" />)s
    /// </summary>
    /// <param name="uIntIn">the uint to convert</param>
    /// <returns>uint containing the value of the uint</returns>
    public static unsafe byte[] UIntToBytes(uint uIntIn)
    {
        //turn a uint into 4 bytes
        var fourBytes = new byte[4];
        var pt = &uIntIn;
        var bt = (byte*)&pt[0];
        fourBytes[0] = *bt++;
        fourBytes[1] = *bt++;
        fourBytes[2] = *bt++;
        fourBytes[3] = *bt++;
        return fourBytes;
    }

    /// <summary>
    ///     Utility function to round any number to any arbirary boundary
    /// </summary>
    /// <param name="number">number to be rounded</param>
    /// <param name="boundary">boundary multiplier</param>
    /// <returns>new rounded number</returns>
    public static uint UtilRoundToBoundary(uint number, uint boundary)
    {
        var newNumber = (uint)(boundary * (number / boundary + (number % boundary > 0 ? 1 : 0)));
        return newNumber;
    }

    #endregion

    #region Instruction Implementation


    /// <summary>
    ///     Do we output debug for Instructions?
    /// </summary>
    private readonly bool _dumpInstructions;

    /// <summary>
    ///     Memory manager passed in by the OS
    /// </summary>
    public MemoryManager MemoryManager { get; set; } = null;

    /// <summary>
    ///     Running Processes passed in by the cpu
    /// </summary>
    public Processes RunningProcesses { get; set; } = null;

    /// <summary>
    ///     Current Process being executed
    /// </summary>
    public Process CurrentProcess { get; set; } = null;

    /// <summary>
    ///     There are 10 locks, numbered 1 to 10.  Lock 0 is not used.
    ///     We will store 0 when the lock is free, or the ProcessID when the lock is acquired
    /// </summary>
    public uint[] Locks { get; set; } = null;

    /// <summary>
    ///     There are 10 events, numbered 1 to 10.  Event 0 is not used
    /// </summary>
    public EventState[] Events { get; set; } = null;




    /// <summary>
    ///     Utility function to fetch a 4 byte unsigned int from Process Memory based on the current
    ///     <see cref="CPU.InstructionPointer" />
    /// </summary>
    /// <returns>a new uint</returns>
    public uint FetchUIntAndMove()
    {
        var retVal = MemoryManager.GetUIntFrom(CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Incr == instruction);

        //move to the param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        //increment the register pointed to by this memory
        CPU.Registers[register]++;
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Addi == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register} {param1}");
        }

        //increment the register pointed to by this memory by the const next to it
        CPU.Registers[register] += param1;
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Addr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register} {param1}");
        }

        //add 1st register and 2nd register and put the result in 1st register
        CPU.Registers[register] = CPU.Registers[register] + CPU.Registers[param1];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Cmpi == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param1 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register} {param1}");
        }

        //compare register and const
        CPU.ZeroFlag = false;
        if (CPU.Registers[register] < param1)
        {
            CPU.SignFlag = true;
        }

        if (CPU.Registers[register] > param1)
        {
            CPU.SignFlag = false;
        }

        if (CPU.Registers[register] == param1)
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Cmpr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        //compare register and const
        CPU.ZeroFlag = false;
        if (CPU.Registers[register1] < CPU.Registers[register2])
        {
            CPU.SignFlag = true;
        }

        if (CPU.Registers[register1] > CPU.Registers[register2])
        {
            CPU.SignFlag = false;
        }

        if (CPU.Registers[register1] == CPU.Registers[register2])
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Call == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer);

        CPU.InstructionPointer += CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Callm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer);

        CPU.InstructionPointer += MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register]];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Ret == instruction);

        CPU.InstructionPointer++;

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction}");
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jmp == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();


        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        var instructionsToSkip = (int)CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jlt == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        if (CPU.SignFlag)
        {
            CPU.InstructionPointer += CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Jgt == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        if (CPU.SignFlag == false)
        {
            CPU.InstructionPointer += CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Je == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        if (CPU.ZeroFlag)
        {
            CPU.InstructionPointer += CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Exit == instruction);

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction}");
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movi == instruction);

        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();
        var param2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register} {param2}");
        }

        //move VALUE of param into 1st register 
        CPU.Registers[register] = param2;
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        //move VALUE of 2nd register into 1st register 
        CPU.Registers[register1] = CPU.Registers[register2];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movmr == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        //move VALUE of memory pointed to by 2nd register into 1st register 
        CPU.Registers[register1] =
            MemoryManager.GetUIntFrom(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register2]);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movrm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        //set memory pointed to by register 1 to contents of register2
        MemoryManager.SetUIntAt(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register1],
            CPU.Registers[register2]);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Movmm == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        //set memory point to by register 1 to contents of memory pointed to by register 2
        MemoryManager.SetUIntAt(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register1],
            MemoryManager.GetUIntFrom(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register2]));
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Printr == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        Console.WriteLine(CPU.Registers[register]);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Printm == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        Console.WriteLine(MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register]]);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Input == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        CPU.Registers[register] = uint.Parse(Console.ReadLine());
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Sleep == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        //Set the number of clockCycles to sleep
        CurrentProcess.ProcessControlBlock.SleepCounter = CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.SetPriority == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        CurrentProcess.ProcessControlBlock.Priority =
            (int)Math.Min(CPU.Registers[register], (int)ProcessPriority.MaxPriority);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Pushr == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        StackPush(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register]);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Pushi == instruction);

        //move to the param containing the 1st param
        CPU.InstructionPointer++;
        var param = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} {param}");
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.TerminateProcess == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        foreach (var p in RunningProcesses)
        {
            if (p.ProcessControlBlock.Pid == CPU.Registers[register])
            {
                p.ProcessControlBlock.State = ProcessState.Terminated;
                Console.WriteLine($"Process {CurrentProcess.ProcessControlBlock.Pid} has forceably terminated Process {p.ProcessControlBlock.Pid}");
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Popr == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        CPU.Registers[register] = StackPop(CurrentProcess.ProcessControlBlock.Pid);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.MemoryClear == instruction);

        //move to the param containing the 1st register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        //move VALUE of memory pointed to by 2nd register into 1st register 
        MemoryManager.SetMemoryOfProcess(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register1],
            CPU.Registers[register2], 0);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Popm == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        MemoryManager.SetUIntAt(CurrentProcess.ProcessControlBlock.Pid, CPU.Registers[register],
            StackPop(CurrentProcess.ProcessControlBlock.Pid));
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.AcquireLock == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        //Are we the first ones here? with a valid lock?
        if (CPU.Registers[register] > 0 && CPU.Registers[register] <= 10)
        {
            if (Locks[CPU.Registers[register]] == 0)
            {
                //Set the lock specified in the register as locked...
                Locks[CPU.Registers[register]] = CurrentProcess.ProcessControlBlock.Pid;
            }
            else if (Locks[CPU.Registers[register]] == CurrentProcess.ProcessControlBlock.Pid)
            {
                //No-Op, we already have this lock
                ;
            }
            else
            {
                //Get in line for this lock
                CurrentProcess.ProcessControlBlock.WaitingLock = CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.ReleaseLock == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        //Release only if we already have this lock, and it's a valid lock
        if (CPU.Registers[register] > 0 && CPU.Registers[register] <= 10)
        {
            if (Locks[CPU.Registers[register]] == CurrentProcess.ProcessControlBlock.Pid)
            {
                //set the lock back to 0 (the OS)
                Locks[CPU.Registers[register]] = 0;
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.SignalEvent == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        if (CPU.Registers[register] > 0 && CPU.Registers[register] <= 10)
        {
            Events[CPU.Registers[register]] = EventState.Signaled;
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.WaitEvent == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register}");
        }

        if (CPU.Registers[register] > 0 && CPU.Registers[register] <= 10)
        {
            CurrentProcess.ProcessControlBlock.WaitingEvent = CPU.Registers[register];
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.MapSharedMem == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove();
        var register2 = FetchUIntAndMove();

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        CPU.Registers[register2] =
            MemoryManager.MapSharedMemoryToProcess(CPU.Registers[register1], CurrentProcess.ProcessControlBlock.Pid);
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.Alloc == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove(); //bytes requested
        var register2 = FetchUIntAndMove(); //address returned

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1} r{register2}");
        }

        var addr = MemoryManager.ProcessHeapAlloc(CurrentProcess, CPU.Registers[register1]);

        CPU.Registers[register2] = addr;
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
        var instruction =
            (InstructionType)MemoryManager[CurrentProcess.ProcessControlBlock.Pid, CPU.InstructionPointer];
        Debug.Assert(InstructionType.FreeMemory == instruction);

        //move to the param containing the register
        CPU.InstructionPointer++;
        var register1 = FetchUIntAndMove(); //address of memory

        if (_dumpInstructions)
        {
            Console.WriteLine($" Pid:{CurrentProcess.ProcessControlBlock.Pid} {instruction} r{register1}");
        }

        MemoryManager.ProcessHeapFree(CurrentProcess, CPU.Registers[register1]);
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
        if (CPU.StackPointer < CurrentProcess.ProcessControlBlock.ProcessMemorySize - 1 -
            CurrentProcess.ProcessControlBlock.StackSize)
        {
            throw new StackException(CurrentProcess.ProcessControlBlock.Pid,
                CurrentProcess.ProcessControlBlock.ProcessMemorySize - 1 -
                CurrentProcess.ProcessControlBlock.StackSize - CPU.StackPointer);
        }

        MemoryManager.SetUIntAt(processid, CPU.StackPointer, avalue);
    }

    /// <summary>
    ///     Pop a uint off the stack for this Process
    /// </summary>
    /// <param name="processid">The Process ID</param>
    /// <returns>the uint from the stack</returns>
    public uint StackPop(uint processid)
    {
        var retVal = MemoryManager.GetUIntFrom(processid, CPU.StackPointer);
        MemoryManager.SetMemoryOfProcess(processid, CPU.StackPointer, 4, 0);
        CPU.StackPointer += 4;
        return retVal;
    }
    #endregion
}