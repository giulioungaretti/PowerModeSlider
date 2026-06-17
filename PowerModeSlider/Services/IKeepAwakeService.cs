using System;

namespace PowerModeSlider.Services;

/// <summary>
/// Abstraction for the keep-awake (prevent sleep) feature.
/// </summary>
public interface IKeepAwakeService
{
    /// <summary>
    /// Gets whether the machine is currently being kept awake.
    /// </summary>
    bool IsAwake { get; }

    /// <summary>
    /// Turns keep-awake on or off. When on, the system (and display) is kept
    /// awake indefinitely; when off, normal sleep behavior resumes.
    /// </summary>
    /// <param name="awake"><see langword="true"/> to keep awake, otherwise <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the underlying call succeeded.</returns>
    bool SetAwake(bool awake);
}
