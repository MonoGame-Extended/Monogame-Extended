// Copyright (c) Craftwork Games. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using MonoGame.Extended.Particles.Data;

namespace MonoGame.Extended.Particles;

/// <summary>
/// Represents a circular memory buffer that efficiently stores and manages particles in contiguous unmanaged memory.
/// </summary>
/// <remarks>
/// The <see cref="ParticleBuffer"/> class provides high-performance memory management for particle systems by
/// allocating an unmanaged memory block to store particle data in a circular buffer arrangement. This implementation
/// uses head and tail pointers to manage particle allocation and deallocation without requiring memory copying
/// operations, making it more efficient than a linear buffer approach.
/// </remarks>
public unsafe class ParticleBuffer : IDisposable
{
    private readonly ParticleIterator _iterator;

    /// <summary>
    /// Gets the native pointer to the beginning of the unmanaged memory buffer that stores particle data.
    /// </summary>
    /// <value>
    /// An <see cref="IntPtr"/> pointing to the start of the allocated unmanaged memory block.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property provides direct access to the underlying unmanaged memory used by the particle buffer.
    /// The memory pointed to by this value contains particle data stored in a contiguous block, with each
    /// particle occupying <see cref="Particle.SizeInBytes"/> bytes.
    /// </para>
    /// <para>
    /// <strong>Warning:</strong> This pointer should be used with extreme caution and only within unsafe code blocks.
    /// Improper use of this pointer can lead to memory corruption, access violations, or other undefined behavior.
    /// The memory is automatically managed by this <see cref="ParticleBuffer"/> instance and should not be
    /// manually freed or modified outside of the provided safe methods.
    /// </para>
    /// <para>
    /// The memory layout is arranged as a circular buffer where particles are stored sequentially.
    /// Use <see cref="Head"/> and the <see cref="Iterator"/> for safe access to active particles rather than
    /// directly manipulating this pointer.
    /// </para>
    /// <para>
    /// This pointer becomes invalid after the <see cref="ParticleBuffer"/> is disposed. Accessing it after
    /// disposal will result in undefined behavior.
    /// </para>
    /// </remarks>
    public IntPtr NativePointer { get; }

    /// <summary>
    /// Gets a pointer to the current tail position in the circular buffer where new particles are allocated.
    /// </summary>
    public unsafe Particle* Tail { get; private set; }

    /// <summary>
    /// Gets a pointer to the end fo the allocated buffer memory, used for circular buffer bounds checking.
    /// </summary>
    public Particle* BufferEnd { get; }

    /// <summary>
    /// Gets the maximum number of particles that can be stored in this buffer.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Gets an iterator for traversing the active particles in the buffer.
    /// </summary>
    /// <remarks>
    /// The iterator is reset each time this property is accessed, starting from the current head position.
    /// Use this to safely iterate through all active particles in the correct order.
    /// </remarks>
    public ParticleIterator Iterator => _iterator.Reset();

    /// <summary>
    /// Gets a pointer to the current head position in the circular buffer where the oldest active particle is located.
    /// </summary>
    /// <remarks>
    /// This pointer should be used carefully and only within unsafe code blocks. The memory it points to is
    /// automatically freed when the <see cref="ParticleBuffer"/> is disposed.
    /// </remarks>
    public unsafe Particle* Head { get; private set; }

    /// <summary>
    /// Gets the number of additional particles that can be added to the buffer before it is full.
    /// </summary>
    public int Available => Size - Count;

    /// <summary>
    /// Gets the current number of active particles in the buffer.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gets the total size of the buffer in bytes.
    /// </summary>
    /// <remarks>
    /// The size includes space for one additional particle beyond the specified capacity to facilitate
    /// circular buffer operations.
    /// </remarks>
    public int SizeInBytes => Particle.SizeInBytes * (Size + 1);

    /// <summary>
    /// Gets the size of the currently active portion of the buffer in bytes.
    /// </summary>
    public int ActiveSizeInBytes => Particle.SizeInBytes * Count;

    /// <summary>
    /// Gets a value indicating whether this <see cref="ParticleBuffer"/> has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParticleBuffer"/> class with the specified capacity.
    /// </summary>
    /// <param name="size">The maximum number of particles that can be stored in the buffer.</param>
    public unsafe ParticleBuffer(int size)
    {
        Size = size;
        NativePointer = Marshal.AllocHGlobal(SizeInBytes);

        BufferEnd = (Particle*)(NativePointer + SizeInBytes);
        Head = (Particle*)NativePointer;
        Tail = (Particle*)NativePointer;

        _iterator = new ParticleIterator(this);

        GC.AddMemoryPressure(SizeInBytes);
    }

    /// <summary/>
    ~ParticleBuffer() => Dispose();

    /// <summary>
    /// Allocates space in the circular buffer for a specified number of particles to be released.
    /// </summary>
    /// <remarks>
    /// The allocated slots can contain data from previously released particles. Callers that initialize particles
    /// manually must set every field required by their modifiers before updating or rendering them. In particular,
    /// when using <see cref="MonoGame.Extended.Particles.Modifiers.OpacityFastFadeModifier"/>, initialize both
    /// <see cref="Particle.Opacity"/> and <see cref="Particle.InitialOpacity"/>.
    /// </remarks>
    /// <param name="releaseQuantity">The number of particles to allocate space for.</param>
    /// <returns>
    /// A <see cref="ParticleIterator"/> positioned at the start of the newly allocated particles,
    /// allowing iteration over the allocated particle slots.
    /// </returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this method is called after the buffer has been disposed.
    /// </exception>
    public unsafe ParticleIterator Release(int releaseQuantity)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        int numToRelease = Math.Min(releaseQuantity, Available);

        int prevCount = Count;
        Count += numToRelease;

        Tail += numToRelease;

        if (Tail >= BufferEnd)
        {
            Tail -= Size + 1;
        }

        return Iterator.Reset(prevCount);
    }

    /// <summary>
    /// Removes a specified number of particles from the beginning of the circular buffer.
    /// </summary>
    /// <param name="number">The number of particles to remove from the buffer.</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this method is called after the buffer has been disposed.
    /// </exception>
    public unsafe void Reclaim(int number)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Count -= number;

        Head += number;

        if (Head >= BufferEnd)
        {
            Head -= Size + 1;
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="ParticleBuffer"/>.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Marshal.FreeHGlobal(NativePointer);
        GC.RemoveMemoryPressure(SizeInBytes);
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
