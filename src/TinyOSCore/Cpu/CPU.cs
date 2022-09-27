// ------------------------------------------------------------------------------
// <copyright from='2002' to='2002' company='Scott Hanselman'>
//    Copyright (c) Scott Hanselman. All Rights Reserved.   
// </copyright> 
// ------------------------------------------------------------------------------
//
// Scott Hanselman's Tiny Academic Virtual CPU and OS
// Copyright (c) 2002, Scott Hanselman (scott@hanselman.com)
// All rights reserved.
// 
// A BSD License
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// 
// Redistributions of source code must retain the above copyright notice, 
// this list of conditions and the following disclaimer. 
// Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation 
// and/or other materials provided with the distribution. 
// Neither the name of Scott Hanselman nor the names of its contributors
// may be used to endorse or promote products derived from this software without
// specific prior written permission. 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" 
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS 
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE 
// OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//

using System;
using System.Collections;
using System.Diagnostics;
using TinyOSCore.Core;
using TinyOSCore.Exceptions;

namespace TinyOSCore.Cpu;

/// <summary>
///     CPU is never instanciated, but is "always" there...like a real CPU. :)  It holds <see cref="physicalMemory" />
///     and the <see cref="registers" />.  It also provides a mapping from <see cref="Instruction" />s to SystemCalls in
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
    public static uint pageSize;

    /// <summary>
    ///     The clock for the system.  This increments as we execute each <see cref="Instruction" />.
    /// </summary>
    public static uint clock;

    /// <summary>
    ///     The CPU's reference to the <see cref="OS" />.  This is set by the <see cref="EntryPoint" />.
    /// </summary>
    public static OS theOS = null;

    /// <summary>
    ///     Here is the actual array of bytes that contains the physical memory for this CPU.
    /// </summary>
    internal static byte[] physicalMemory;

    /// <summary>
    ///     We have 10 registers.  R11 is the <see cref="InstructionPointer" />, and we don't use R0.  R10 is the <see cref="StackPointer" />.  So,
    ///     that's 1 to 10, and 11.
    /// </summary>
    internal static uint[] registers = new uint[12]; //0 to 11

    /// <summary>
    ///     We have a Sign Flag and a Zero Flag in a <see cref="BitArray" />
    /// </summary>
    private static readonly BitArray BitFlagRegisters = new BitArray(2, false);

    /// <summary>
    ///     Initialized our <see cref="physicalMemory" /> array that represents physical memory.  Should only be called once.
    /// </summary>
    /// <param name="memorySize">The size of physical memory</param>
    public static void InitPhysicalMemory(uint memorySize)
    {
        pageSize = uint.Parse(EntryPoint.Configuration["MemoryPageSize"]);

        var newMemorySize = UtilRoundToBoundary(memorySize, pageSize);

        // Initalize Physical Memory
        physicalMemory = new byte[newMemorySize];

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
        OpCodeToSysCall((InstructionType)theOS.MemoryMgr[theOS.CurrentProcess.ProcessControlBlock.Pid, InstructionPointer]);
        clock++;
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
            theOS.Noop, //0

            theOS.Incr, //1
            theOS.Addi, //2
            theOS.Addr, //3
            theOS.Pushr, //4
            theOS.Pushi, //5

            theOS.Movi, //6
            theOS.Movr, //7
            theOS.Movmr, //8
            theOS.Movrm, //9
            theOS.Movmm, //10

            theOS.Printr, //11
            theOS.Printm, //12
            theOS.Jmp, //13
            theOS.Cmpi, //14
            theOS.Cmpr, //15

            theOS.Jlt, //16
            theOS.Jgt, //17
            theOS.Je, //18
            theOS.Call, //19
            theOS.Callm, //20

            theOS.Ret, //21
            theOS.Alloc, //22
            theOS.AcquireLock, //23
            theOS.ReleaseLock, //24
            theOS.Sleep, //25

            theOS.SetPriority, //26
            theOS.Exit, //27
            theOS.FreeMemory, //28
            theOS.MapSharedMem, //29
            theOS.SignalEvent, //30

            theOS.WaitEvent, //31
            theOS.Input, //32
            theOS.MemoryClear, //33
            theOS.TerminateProcess, //34
            theOS.Popr, //35

            theOS.Popm //36
        };

        #endregion

        Debug.Assert(opCode >= InstructionType.Incr && opCode <= InstructionType.Popm);

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
        get => registers[10];
        set => registers[10] = value;
    }

    /// <summary>
    ///     Public get/set access for the CPU's Instruction Pointer
    /// </summary>
    public static uint InstructionPointer
    {
        get => registers[11];
        set => registers[11] = value;
    }

    #endregion


    #region Dump Functions for debugging

    /// <summary>
    ///     Dumps the values of <see cref="registers" /> as the <see cref="CPU" /> currently sees it.
    /// </summary>
    public static void DumpRegisters()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpRegisters"]) == false)
        {
            return;
        }

        Console.WriteLine($"CPU Registers: r1 {registers[1],-8:G}          r6  {registers[6],-8:G}");
        Console.WriteLine($"               r2 {registers[2],-8:G}          r7  {registers[7],-8:G}");
        Console.WriteLine($"               r3 {registers[3],-8:G}    (pid) r8  {registers[8],-8:G}");
        Console.WriteLine($"               r4 {registers[4],-8:G}   (data) r9 . {registers[9],-8:G}");
        Console.WriteLine($"               r5 {registers[5],-8:G}     (sp) r10 {registers[10]}");
        Console.WriteLine($"               sf {SignFlag,-8:G}          ip  {InstructionPointer}");
        Console.WriteLine($"               zf {ZeroFlag,-8:G}      ");
    }

    /// <summary>
    ///     Dumps the current <see cref="Instruction" /> for the current process at the current <see cref="InstructionPointer" />
    /// </summary>
    public static void DumpInstruction()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpInstruction"]) == false)
        {
            return;
        }

        Console.WriteLine($" Pid:{registers[8]} {(InstructionType)theOS.MemoryMgr[theOS.CurrentProcess.ProcessControlBlock.Pid, InstructionPointer]} {(uint)theOS.MemoryMgr[theOS.CurrentProcess.ProcessControlBlock.Pid, InstructionPointer]}");
    }

    /// <summary>
    ///     Dumps the content of the CPU's <see cref="physicalMemory" /> array.
    /// </summary>
    public static void DumpPhysicalMemory()
    {
        if (bool.Parse(EntryPoint.Configuration["DumpPhysicalMemory"]) == false)
        {
            return;
        }

        var address = 0;
        foreach (var b in physicalMemory)
        {
            if (address == 0 || address % 16 == 0)
            {
                Console.Write(Environment.NewLine + "{0,-4:000} ", address);
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