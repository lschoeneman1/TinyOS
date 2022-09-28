using System;
using System.Collections;
using System.Diagnostics;
using TinyOSCore.Core;

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
            TheOS.Noop, //0

            TheOS.Incr, //1
            TheOS.Addi, //2
            TheOS.Addr, //3
            TheOS.Pushr, //4
            TheOS.Pushi, //5

            TheOS.Movi, //6
            TheOS.Movr, //7
            TheOS.Movmr, //8
            TheOS.Movrm, //9
            TheOS.Movmm, //10

            TheOS.Printr, //11
            TheOS.Printm, //12
            TheOS.Jmp, //13
            TheOS.Cmpi, //14
            TheOS.Cmpr, //15

            TheOS.Jlt, //16
            TheOS.Jgt, //17
            TheOS.Je, //18
            TheOS.Call, //19
            TheOS.Callm, //20

            TheOS.Ret, //21
            TheOS.Alloc, //22
            TheOS.AcquireLock, //23
            TheOS.ReleaseLock, //24
            TheOS.Sleep, //25

            TheOS.SetPriority, //26
            TheOS.Exit, //27
            TheOS.FreeMemory, //28
            TheOS.MapSharedMem, //29
            TheOS.SignalEvent, //30

            TheOS.WaitEvent, //31
            TheOS.Input, //32
            TheOS.MemoryClear, //33
            TheOS.TerminateProcess, //34
            TheOS.Popr, //35

            TheOS.Popm //36
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
}