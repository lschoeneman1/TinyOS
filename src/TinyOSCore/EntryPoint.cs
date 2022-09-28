using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using TinyOSCore.Core;
using TinyOSCore.Cpu;

namespace TinyOSCore;

/// <summary>
///     "Bootstraps" the system by creating an <see cref="OS" />, setting the size of the <see cref="CPU" />'s memory,
///     and loading each <see cref="Program" /> into memory.  Then, for each <see cref="Program" /> we create a
///     <see cref="Process" />.  Then we start everything by calling <see cref="OS.Execute" />
/// </summary>
internal class EntryPoint
{
    public static IConfigurationRoot Configuration { get; set; }

    /// <summary>
    ///     The entry point for the virtual OS
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
        Configuration = builder.Build();

        PrintHeader();

        if (args.Length < 2)
        {
            PrintInstructions();
        }
        else
        {
            // Total addressable (virtual) memory taken from the command line
            var bytesOfVirtualMemory = uint.Parse(args[0]);

            var bytesOfPhysicalMemory = uint.Parse(Configuration["PhysicalMemory"]);

            // Setup static physical memory
            CPU.InitPhysicalMemory(bytesOfPhysicalMemory);

            // Create the OS and Memory Manager with Virtual Memory
            var theOS = new OS(bytesOfVirtualMemory);

            // Let the CPU know about the OS
            CPU.TheOS = theOS;

            Console.WriteLine("CPU has {0} bytes of physical memory", CPU.PhysicalMemory.Length);
            Console.WriteLine("OS  has {0} bytes of virtual (addressable) memory", theOS.MemoryMgr.VirtualMemSize);

            // For each file on the command line, load the program and create a process
            for (var i = 1; i < args.Length; i++)
            {
                if (File.Exists(args[i]))
                {
                    var p = Program.LoadProgram(args[i]);
                    var rp = theOS.CreateProcess(p, uint.Parse(Configuration["ProcessMemory"]));
                    Console.WriteLine("Process id {0} has {1} bytes of process memory and {2} bytes of heap",
                        rp.ProcessControlBlock.Pid, Configuration["ProcessMemory"],
                        rp.ProcessControlBlock.HeapAddrEnd - rp.ProcessControlBlock.HeapAddrStart);
                    p.DumpProgram();
                }
            }

            // Start executing!
            theOS.Execute();

            // Pause
            Console.WriteLine("OS execution complete.  Press Enter to continue...");
            Console.ReadLine();
        }
    }

    /// <summary>
    ///     Prints the static instructions on how to invoke from the command line
    /// </summary>
    private static void PrintInstructions()
    {
        Console.WriteLine("");
        Console.WriteLine("usage: OS membytes [files]");
    }

    /// <summary>
    ///     Prints the static informatonal header
    /// </summary>
    private static void PrintHeader()
    {
        Console.WriteLine("CSCI 480 Mini Operating System");
        Console.WriteLine(Assembly.GetExecutingAssembly().FullName);
    }
}