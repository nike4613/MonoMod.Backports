﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Buffers
{
    /// <summary>
    /// Provides a resource pool that enables reusing instances of arrays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Renting and returning buffers with an <see cref="ArrayPool{T}"/> can increase performance
    /// in situations where arrays are created and destroyed frequently, resulting in significant
    /// memory pressure on the garbage collector.
    /// </para>
    /// <para>
    /// This class is thread-safe.  All members may be used by multiple threads concurrently.
    /// </para>
    /// </remarks>
    public abstract class ArrayPool<T>
    {
        // Store the shared ArrayPool in a field of its derived sealed type so the Jit can "see" the exact type
        // when the Shared property is inlined which will allow it to devirtualize calls made on it.
        private static readonly SharedArrayPool<T> s_shared = new SharedArrayPool<T>();

        /// <summary>
        /// Retrieves a shared <see cref="ArrayPool{T}"/> instance.
        /// </summary>
        /// <remarks>
        /// The shared pool provides a default implementation of <see cref="ArrayPool{T}"/>
        /// that's intended for general applicability.  It maintains arrays of multiple sizes, and
        /// may hand back a larger array than was actually requested, but will never hand back a smaller
        /// array than was requested. Renting a buffer from it with <see cref="Rent"/> will result in an
        /// existing buffer being taken from the pool if an appropriate buffer is available or in a new
        /// buffer being allocated if one is not available.
        /// byte[] and char[] are the most commonly pooled array types. For these we use a special pool type
        /// optimized for very fast access speeds, at the expense of more memory consumption.
        /// The shared pool instance is created lazily on first access.
        /// </remarks>
        public static ArrayPool<T> Shared => s_shared;

        /// <summary>
        /// Creates a new <see cref="ArrayPool{T}"/> instance using default configuration options.
        /// </summary>
        /// <returns>A new <see cref="ArrayPool{T}"/> instance.</returns>
        public static ArrayPool<T> Create() => new ConfigurableArrayPool<T>();

        /// <summary>
        /// Creates a new <see cref="ArrayPool{T}"/> instance using custom configuration options.
        /// </summary>
        /// <param name="maxArrayLength">The maximum length of array instances that may be stored in the pool.</param>
        /// <param name="maxArraysPerBucket">
        /// The maximum number of array instances that may be stored in each bucket in the pool.  The pool
        /// groups arrays of similar lengths into buckets for faster access.
        /// </param>
        /// <returns>A new <see cref="ArrayPool{T}"/> instance with the specified configuration options.</returns>
        /// <remarks>
        /// The created pool will group arrays into buckets, with no more than <paramref name="maxArraysPerBucket"/>
        /// in each bucket and with those arrays not exceeding <paramref name="maxArrayLength"/> in length.
        /// </remarks>
        public static ArrayPool<T> Create(int maxArrayLength, int maxArraysPerBucket) =>
            new ConfigurableArrayPool<T>(maxArrayLength, maxArraysPerBucket);

        /// <summary>
        /// Retrieves a buffer that is at least the requested length.
        /// </summary>
        /// <param name="minimumLength">The minimum length of the array needed.</param>
        /// <returns>
        /// An array that is at least <paramref name="minimumLength"/> in length.
        /// </returns>
        /// <remarks>
        /// This buffer is loaned to the caller and should be returned to the same pool via
        /// <see cref="Return(T[], bool)"/> so that it may be reused in subsequent usage of <see cref="Rent"/>.
        /// It is not a fatal error to not return a rented buffer, but failure to do so may lead to
        /// decreased application performance, as the pool may need to create a new buffer to replace
        /// the one lost.
        /// </remarks>
        public abstract T[] Rent(int minimumLength);

        /// <summary>
        /// Returns to the pool an array that was previously obtained via <see cref="Rent"/> on the same
        /// <see cref="ArrayPool{T}"/> instance.
        /// </summary>
        /// <param name="array">
        /// The buffer previously obtained from <see cref="Rent"/> to return to the pool.
        /// </param>
        /// <param name="clearArray">
        /// If <c>true</c> and if the pool will store the buffer to enable subsequent reuse, <see cref="Return(T[], bool)"/>
        /// will clear <paramref name="array"/> of its contents so that a subsequent consumer via <see cref="Rent"/>
        /// will not see the previous consumer's content.  If <c>false</c> or if the pool will release the buffer,
        /// the array's contents are left unchanged.
        /// </param>
        /// <remarks>
        /// Once a buffer has been returned to the pool, the caller gives up all ownership of the buffer
        /// and must not use it. The reference returned from a given call to <see cref="Rent"/> must only be
        /// returned via <see cref="Return(T[], bool)"/> once.  The default <see cref="ArrayPool{T}"/>
        /// may hold onto the returned buffer in order to rent it again, or it may release the returned buffer
        /// if it's determined that the pool already has enough buffers stored.
        /// </remarks>
        public abstract void Return(T[] array, bool clearArray = false);

        internal void Return(T[] array, int lengthToClear)
        {
            array.AsSpan(0, lengthToClear).Clear();
            Return(array);
        }
    }
}