// Copyright (c) Craftwork Games. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MonoGame.Extended.Particles.Data;

/// <summary>
/// Represents an individual particle within the particle system.
/// </summary>
/// <remarks>
/// The struct uses sequential layout with tight packing to optimize memory usage and performance.
/// The fixed arrays are used to store positional, velocity, and color data efficiently in unmanaged memory.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Particle
{
    /// <summary>
    /// The time (in seconds) when this particle was created.
    /// </summary>
    public float Inception;

    /// <summary>
    /// The current age (in seconds) of this particle.
    /// </summary>
    public float Age;

    /// <summary>
    /// The current position of this particle in 2D space [X, Y].
    /// </summary>
    public fixed float Position[2];

    /// <summary>
    /// The current velocity vector of this particle [X, Y].
    /// </summary>
    public fixed float Velocity[2];

    /// <summary>
    /// The color of this particle in RGB format [R, G, B].
    /// </summary>
    public fixed float Color[3];

    /// <summary>
    /// The scale factor applied to this particle's visual representation.
    /// </summary>
    public fixed float Scale[2];

    /// <summary>
    /// The position where this particle was triggered or emitted from [X, Y].
    /// </summary>
    public fixed float TriggeredPos[2];

    /// <summary>
    /// The current mutable opacity (alpha) value of this particle, ranging from 0.0 (transparent) to 1.0 (opaque).
    /// </summary>
    public float Opacity;

    /// <summary>
    /// The opacity (alpha) value assigned to this particle at release time.
    /// </summary>
    /// <remarks>
    /// <see cref="MonoGame.Extended.Particles.Modifiers.OpacityFastFadeModifier"/> uses this value as the baseline when calculating
    /// <see cref="Opacity"/>. <see cref="ParticleEmitter"/> initializes this field from <see cref="Opacity"/>
    /// when it releases a particle. Callers that obtain particles directly from <see cref="ParticleBuffer"/> must
    /// set this field with the desired initial opacity when using that modifier.
    /// </remarks>
    public float InitialOpacity;

    /// <summary>
    /// The rotation of this particle in radians.
    /// </summary>
    public float Rotation;

    /// <summary>
    /// The mass of this particle used during physics calculations.
    /// </summary>
    public float Mass;

    /// <summary>
    /// The depth at which this particle is rendered, used for layering particles.
    /// </summary>
    /// <remarks>
    /// Values range from 0.0 (front) to 1.0 (back).
    /// </remarks>
    public float LayerDepth;

    /// <summary>
    /// The size of the <see cref="Particle"/> struct in bytes.
    /// </summary>
    /// <remarks>
    /// Used for memory allocations and buffer operations.
    /// </remarks>
    public static readonly int SizeInBytes = Marshal.SizeOf<Particle>();
}
