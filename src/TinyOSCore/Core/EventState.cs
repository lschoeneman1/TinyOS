namespace TinyOSCore.Core;

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