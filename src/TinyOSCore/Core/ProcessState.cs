namespace TinyOSCore.Core;

/// <summary>
///     All the states a <see cref="Process" /> can experience.
/// </summary>
public enum ProcessState
{
    /// <summary>
    ///     A <see cref="Process" /> initial state
    /// </summary>
    NewProcess = 0,

    /// <summary>
    ///     The state of a <see cref="Process" /> ready to run
    /// </summary>
    Ready,

    /// <summary>
    ///     The state of the currently running <see cref="Process" />
    /// </summary>
    Running,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting after a Sleep
    /// </summary>
    WaitingAsleep,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting after an AcquireLock
    /// </summary>
    WaitingOnLock,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting after a WaitEvent
    /// </summary>
    WaitingOnEvent,

    /// <summary>
    ///     The state of a <see cref="Process" /> waiting to be removed from the Running <see cref="ProcessCollection" />
    /// </summary>
    Terminated
}