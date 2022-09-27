namespace TinyOSCore.Core;

/// <summary>
///     The Range of <see cref="Process.ProcessControlBlock.priority" /> a <see cref="Process" /> can experience.
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