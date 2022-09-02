using System;
using System.Collections.Generic;
using System.IO;

namespace TinyOSCore;

/// <summary>
///     Represents a Program (not a <see cref="Process" />) on disk and the <see cref="Instruction" />s it's comprised of.
///     Used as a utility class to load a <see cref="Program" /> off disk.
/// </summary>
public class Program
{
    private readonly InstructionCollection _instructions;

    /// <summary>
    ///     Public constructor for a Program
    /// </summary>
    /// <param name="instructionsParam">The collection of <see cref="Instruction" /> objects that make up this Program</param>
    public Program(InstructionCollection instructionsParam)
    {
        _instructions = new InstructionCollection(instructionsParam);
    }

    /// <summary>
    ///     Spins through the <see cref="InstructionCollection" /> and creates an array of bytes
    ///     that is then copied into Memory by <see cref="OS.CreateProcess" />
    /// </summary>
    /// <returns>Array of bytes representing the <see cref="Program" /> in memory</returns>
    public byte[] GetMemoryImage()
    {
        var instructionList = new List<byte>();

        foreach (var instr in _instructions)
        {
            // Instructions are one byte
            instructionList.Add((byte)instr.OpCode);

            // Params are Four Bytes
            if (instr.OpCodeParameter1 != uint.MaxValue)
            {
                var paramBytes = CPU.UIntToBytes(instr.OpCodeParameter1);
                foreach (var t in paramBytes)
                {
                    instructionList.Add(t);
                }
            }

            if (instr.OpCodeParameter2 != uint.MaxValue)
            {
                var paramBytes = CPU.UIntToBytes(instr.OpCodeParameter2);
                foreach (var t in paramBytes)
                {
                    instructionList.Add(t);
                }
            }
        }

        // Create and array of bytes and return the instructions in it
        //instructionList.TrimToSize();
        var arrayInstr = new byte[instructionList.Count];
        instructionList.CopyTo(arrayInstr);
        return arrayInstr;
    }

    /// <summary>
    ///     Loads a Program from a file on disk.  For each line the Program, create an <see cref="Instruction" />
    ///     and pass the raw string to the Instructions's constructor.  The resulting <see cref="InstructionCollection" />
    ///     is the Program
    /// </summary>
    /// <param name="fileName">file with code to load</param>
    /// <returns>a new loaded Program</returns>
    public static Program LoadProgram(string fileName)
    {
        using TextReader reader = File.OpenText(fileName);
        var instructions = new InstructionCollection();
        var rawInstruction = reader.ReadLine();
        while (rawInstruction != null)
        {
            instructions.Add(new Instruction(rawInstruction));
            rawInstruction = reader.ReadLine();
        }

        var program = new Program(instructions);
        reader.Close();
        return program;
    }

    /// <summary>
    ///     For Debugging, pretty prints the Instructions that make up this Program
    /// </summary>
    public void DumpProgram()
    {
        if (!bool.Parse(EntryPoint.Configuration["DumpProgram"]))
        {
            return;
        }

        foreach (var i in _instructions)
        {
            Console.WriteLine(i.ToString());
        }

        Console.WriteLine();
    }
}