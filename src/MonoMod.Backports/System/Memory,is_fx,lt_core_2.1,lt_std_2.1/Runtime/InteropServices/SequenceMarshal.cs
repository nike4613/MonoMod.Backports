﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Provides a collection of methods for interoperating with <see cref="ReadOnlySequence{T}"/>
    /// </summary>
    public static partial class SequenceMarshal
    {
        /// <summary>
        /// Get <see cref="ReadOnlySequenceSegment{T}"/> from the underlying <see cref="ReadOnlySequence{T}"/>.
        /// If unable to get the <see cref="ReadOnlySequenceSegment{T}"/>, return false.
        /// </summary>
        public static bool TryGetReadOnlySequenceSegment<T>(ReadOnlySequence<T> sequence,
            out ReadOnlySequenceSegment<T>? startSegment,
            out int startIndex,
            out ReadOnlySequenceSegment<T>? endSegment,
            out int endIndex)
        {
            return sequence.TryGetReadOnlySequenceSegment(out startSegment, out startIndex, out endSegment, out endIndex);
        }

        /// <summary>
        /// Get an array segment from the underlying <see cref="ReadOnlySequence{T}"/>.
        /// If unable to get the array segment, return false with a default array segment.
        /// </summary>
        public static bool TryGetArray<T>(ReadOnlySequence<T> sequence, out ArraySegment<T> segment)
        {
            return sequence.TryGetArray(out segment);
        }

        /// <summary>
        /// Get <see cref="ReadOnlyMemory{T}"/> from the underlying <see cref="ReadOnlySequence{T}"/>.
        /// If unable to get the <see cref="ReadOnlyMemory{T}"/>, return false.
        /// </summary>
        public static bool TryGetReadOnlyMemory<T>(ReadOnlySequence<T> sequence, out ReadOnlyMemory<T> memory)
        {
            if (!sequence.IsSingleSegment)
            {
                memory = default;
                return false;
            }

            memory = sequence.First;
            return true;
        }

        /// <summary>
        /// Get <see cref="string"/> from the underlying <see cref="ReadOnlySequence{T}"/>.
        /// If unable to get the <see cref="string"/>, return false.
        /// </summary>
        internal static bool TryGetString(ReadOnlySequence<char> sequence, [MaybeNullWhen(false)] out string text, out int start, out int length)
        {
            return sequence.TryGetString(out text, out start, out length);
        }
    }
}