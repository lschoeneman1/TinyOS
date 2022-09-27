using System.Text.RegularExpressions;

namespace TinyOSCore.Cpu;

/// <summary>
///     Represents a single line in a program, consisting of an OpCode
///     and one or two optional parameters.  An instruction can parse a raw instruction from a test file.
///     Tge instruction is then loaded into an InstructionCollection which is a member of
///     Program.  The InstructionCollection is translated into bytes that are
///     loaded into the processes memory space.  It's never used again, but it's a neat overly object oriented
///     construct that simplified the coding of the creation of a Program and complicated the
///     running of the whole system.  It was worth it though.
/// </summary>
public class Instruction
{
    /// <summary>
    ///     Overridden method for pretty printing of Instructions
    /// </summary>
    /// <returns>A formatted string representing an Instruction</returns>
    public override string ToString()
    {
        return
            $"OpCode: {(byte)OpCode,-2:G} {OpCode,-12:G}   Param1: {(Param1 == uint.MaxValue ? "" : Param1.ToString())}   Param2: {(Param2 == uint.MaxValue ? "" : Param2.ToString())}";
    }

    /// <summary>
    ///     The OpCode for this Instruction
    /// </summary>
    public InstructionType OpCode { get; set; }

    /// <summary>
    ///     The first parameter to the opCode.  May be a Constant or a Register value, or not used at all
    /// </summary>
    public uint Param1 { get; set; } = uint.MaxValue;

    /// <summary>
    ///     The second parameter to the opCode.  May be a Constant or a Register value, or not used at all
    /// </summary>
    public uint Param2 { get; set; } = uint.MaxValue;

    /// <summary>
    ///     Public constructor for an Instruction
    /// </summary>
    /// <param name="rawInstruction">A raw string from a Program File.</param>
    /// <example>
    ///     Any one of the following lines is a valid rawInstruction
    ///     <pre>
    ///         1   r1          ; incr r1
    ///         2   r6, $16     ; add 16 to r6
    ///         26  r6          ; setPriority to r6
    ///         2   r2, $5      ; increment r2 by 5
    ///         3   r1, r2      ; add 1 and 2 and the result goes in 1
    ///         2   r2, $5      ; increment r2 by 5
    ///         6   r3, $99     ; move 99 into r3
    ///         7   r4, r3      ; move r3 into r4
    ///         11  r4          ; print r4
    ///         27              ; this is exit.
    ///     </pre>
    /// </example>
    public Instruction(string rawInstruction)
    {
        var r = new Regex("(?:;.+)|\\A(?<opcode>\\d+){1}|\\sr(?<param>[-]*\\d)|\\$(?<const>[-]*\\d+)");

        var matchcol = r.Matches(rawInstruction);
        foreach (Match m in matchcol)
        {
            var g = m.Groups;

            for (var i = 1; i < g.Count; i++)
            {
                if (g[i].Value.Length != 0)
                {
                    if (r.GroupNameFromNumber(i) == "opcode") OpCode = (InstructionType)byte.Parse(g[i].Value);

                    if (r.GroupNameFromNumber(i) == "param" || r.GroupNameFromNumber(i) == "const")
                    {
                        //Yank them as ints (to preserve signed-ness)
                        // Treat them as uints for storage
                        // This will only affect negative numbers, and 
                        // VERY large unsigned numbers
                        if (uint.MaxValue == Param1)
                        {
                            Param1 = uint.Parse(g[i].Value);
                        }
                        else if (uint.MaxValue == Param2)
                        {
                            if (g[i].Value[0] == '-')
                                Param2 = (uint)int.Parse(g[i].Value);
                            else
                                Param2 = uint.Parse(g[i].Value);
                        }
                    }
                }
            }
        }
    }
}