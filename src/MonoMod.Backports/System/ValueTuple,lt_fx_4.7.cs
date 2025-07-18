﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1141 // explicitly not using tuple syntax in tuple implementation

#pragma warning disable CA1036 // Override methods on comparable types
#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA2231 // Overload operator equals on overriding value type Equals
#pragma warning disable CA2201 // Do not raise reserved exception types
// For some reason, the BCL implementation doesn't do any of the above either.

#pragma warning disable IDE0008 // Use explicit type
#pragma warning disable IDE0046
#pragma warning disable IDE0038 // Use pattern matching
// Most of this code is copied directly from the BCL.

#pragma warning disable CA1051 // Do not declare visible instance fields
// One of the major points of ValueTuple is the visible instance fields

namespace System.Runtime.CompilerServices
{
    /// <summary>
    ///     Indicates that the use of <see cref="ValueTuple" /> on a member is meant to be treated as a tuple with
    ///     element names.
    /// </summary>
    [CLSCompliant(false)]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Event)]
    public sealed class TupleElementNamesAttribute : Attribute
    {
        private readonly string[] _transformNames;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TupleElementNamesAttribute" />
        ///     class.
        /// </summary>
        /// <param name="transformNames">
        ///     Specifies, in a pre-order depth-first traversal of a type's
        ///     construction, which <see cref="ValueType" /> occurrences are
        ///     meant to carry element names.
        /// </param>
        /// <remarks>
        ///     This constructor is meant to be used on types that contain an
        ///     instantiation of <see cref="ValueType" /> that contains
        ///     element names.  For instance, if <c>C</c> is a generic type with
        ///     two type parameters, then a use of the constructed type
        ///     <c>C{<see cref="ValueTuple{T1, T2}" />, <see cref="ValueTuple{T1, T2, T3}" /></c> might be intended to
        ///     treat the first type argument as a tuple with element names and the
        ///     second as a tuple without element names. In which case, the
        ///     appropriate attribute specification should use a
        ///     <c>transformNames</c> value of
        ///     <code>
        ///         { "name1", "name2", null, null,
        ///         null }
        ///     </code>
        ///     .
        /// </remarks>
        public TupleElementNamesAttribute(string[] transformNames)
        {
            _transformNames = transformNames ?? throw new ArgumentNullException(nameof(transformNames));
        }

        /// <summary>
        ///     Specifies, in a pre-order depth-first traversal of a type's
        ///     construction, which <see cref="ValueTuple" /> elements are
        ///     meant to carry element names.
        /// </summary>
        public IList<string> TransformNames => _transformNames;
    }
}

namespace System
{
    /// <summary>
    /// Helper so we can call some tuple methods recursively without knowing the underlying types.
    /// </summary>
    internal interface IValueTupleInternal : ITuple
    {
        int GetHashCode(IEqualityComparer comparer);
        string ToStringEnd();
    }

    /// <summary>
    /// The ValueTuple types (from arity 0 to 8) comprise the runtime implementation that underlies tuples in C# and struct tuples in F#.
    /// Aside from created via language syntax, they are most easily created via the ValueTuple.Create factory methods.
    /// The System.ValueTuple types differ from the System.Tuple types in that:
    /// - they are structs rather than classes,
    /// - they are mutable rather than readonly, and
    /// - their members (such as Item1, Item2, etc) are fields rather than properties.
    /// </summary>
    [Serializable]
    public struct ValueTuple
        : IEquatable<ValueTuple>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="ValueTuple"/>.</returns>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple;
        }

        /// <summary>Returns a value indicating whether this instance is equal to a specified value.</summary>
        /// <param name="other">An instance to compare to this instance.</param>
        /// <returns>true if <paramref name="other"/> has the same value as this instance; otherwise, false.</returns>
        public bool Equals(ValueTuple other)
        {
            return true;
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
        {
            return other is ValueTuple;
        }

        int IComparable.CompareTo(object? other)
        {
            if (other is null)
                return 1;

            if (other is not ValueTuple)
            {
                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 0;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple other)
        {
            return 0;
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is null)
                return 1;

            if (other is not ValueTuple)
            {
                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 0;
        }

        /// <summary>Returns the hash code for this instance.</summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return 0;
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return 0;
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return 0;
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>()</c>.
        /// </remarks>
        public override string ToString()
        {
            return "()";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 0;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] => throw new IndexOutOfRangeException();

        /// <summary>Creates a new struct 0-tuple.</summary>
        /// <returns>A 0-tuple.</returns>
        public static ValueTuple Create() =>
            default;

        /// <summary>Creates a new struct 1-tuple, or singleton.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <returns>A 1-tuple (singleton) whose value is (item1).</returns>
        public static ValueTuple<T1> Create<T1>(T1 item1) =>
            new ValueTuple<T1>(item1);

        /// <summary>Creates a new struct 2-tuple, or pair.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <returns>A 2-tuple (pair) whose value is (item1, item2).</returns>
        public static ValueTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2) =>
            new ValueTuple<T1, T2>(item1, item2);

        /// <summary>Creates a new struct 3-tuple, or triple.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <param name="item3">The value of the third component of the tuple.</param>
        /// <returns>A 3-tuple (triple) whose value is (item1, item2, item3).</returns>
        public static ValueTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3) =>
            new ValueTuple<T1, T2, T3>(item1, item2, item3);

        /// <summary>Creates a new struct 4-tuple, or quadruple.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
        /// <typeparam name="T4">The type of the fourth component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <param name="item3">The value of the third component of the tuple.</param>
        /// <param name="item4">The value of the fourth component of the tuple.</param>
        /// <returns>A 4-tuple (quadruple) whose value is (item1, item2, item3, item4).</returns>
        public static ValueTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) =>
            new ValueTuple<T1, T2, T3, T4>(item1, item2, item3, item4);

        /// <summary>Creates a new struct 5-tuple, or quintuple.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
        /// <typeparam name="T4">The type of the fourth component of the tuple.</typeparam>
        /// <typeparam name="T5">The type of the fifth component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <param name="item3">The value of the third component of the tuple.</param>
        /// <param name="item4">The value of the fourth component of the tuple.</param>
        /// <param name="item5">The value of the fifth component of the tuple.</param>
        /// <returns>A 5-tuple (quintuple) whose value is (item1, item2, item3, item4, item5).</returns>
        public static ValueTuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) =>
            new ValueTuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);

        /// <summary>Creates a new struct 6-tuple, or sextuple.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
        /// <typeparam name="T4">The type of the fourth component of the tuple.</typeparam>
        /// <typeparam name="T5">The type of the fifth component of the tuple.</typeparam>
        /// <typeparam name="T6">The type of the sixth component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <param name="item3">The value of the third component of the tuple.</param>
        /// <param name="item4">The value of the fourth component of the tuple.</param>
        /// <param name="item5">The value of the fifth component of the tuple.</param>
        /// <param name="item6">The value of the sixth component of the tuple.</param>
        /// <returns>A 6-tuple (sextuple) whose value is (item1, item2, item3, item4, item5, item6).</returns>
        public static ValueTuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) =>
            new ValueTuple<T1, T2, T3, T4, T5, T6>(item1, item2, item3, item4, item5, item6);

        /// <summary>Creates a new struct 7-tuple, or septuple.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
        /// <typeparam name="T4">The type of the fourth component of the tuple.</typeparam>
        /// <typeparam name="T5">The type of the fifth component of the tuple.</typeparam>
        /// <typeparam name="T6">The type of the sixth component of the tuple.</typeparam>
        /// <typeparam name="T7">The type of the seventh component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <param name="item3">The value of the third component of the tuple.</param>
        /// <param name="item4">The value of the fourth component of the tuple.</param>
        /// <param name="item5">The value of the fifth component of the tuple.</param>
        /// <param name="item6">The value of the sixth component of the tuple.</param>
        /// <param name="item7">The value of the seventh component of the tuple.</param>
        /// <returns>A 7-tuple (septuple) whose value is (item1, item2, item3, item4, item5, item6, item7).</returns>
        public static ValueTuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) =>
            new ValueTuple<T1, T2, T3, T4, T5, T6, T7>(item1, item2, item3, item4, item5, item6, item7);

        /// <summary>Creates a new struct 8-tuple, or octuple.</summary>
        /// <typeparam name="T1">The type of the first component of the tuple.</typeparam>
        /// <typeparam name="T2">The type of the second component of the tuple.</typeparam>
        /// <typeparam name="T3">The type of the third component of the tuple.</typeparam>
        /// <typeparam name="T4">The type of the fourth component of the tuple.</typeparam>
        /// <typeparam name="T5">The type of the fifth component of the tuple.</typeparam>
        /// <typeparam name="T6">The type of the sixth component of the tuple.</typeparam>
        /// <typeparam name="T7">The type of the seventh component of the tuple.</typeparam>
        /// <typeparam name="T8">The type of the eighth component of the tuple.</typeparam>
        /// <param name="item1">The value of the first component of the tuple.</param>
        /// <param name="item2">The value of the second component of the tuple.</param>
        /// <param name="item3">The value of the third component of the tuple.</param>
        /// <param name="item4">The value of the fourth component of the tuple.</param>
        /// <param name="item5">The value of the fifth component of the tuple.</param>
        /// <param name="item6">The value of the sixth component of the tuple.</param>
        /// <param name="item7">The value of the seventh component of the tuple.</param>
        /// <param name="item8">The value of the eighth component of the tuple.</param>
        /// <returns>An 8-tuple (octuple) whose value is (item1, item2, item3, item4, item5, item6, item7, item8).</returns>
        public static ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> Create<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) =>
            new ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>>(item1, item2, item3, item4, item5, item6, item7, ValueTuple.Create(item8));
    }

    /// <summary>Represents a 1-tuple, or singleton, as a value type.</summary>
    /// <typeparam name="T1">The type of the tuple's only component.</typeparam>
    [Serializable]
    public struct ValueTuple<T1>
        : IEquatable<ValueTuple<T1>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1}"/> instance's first component.
        /// </summary>
        public T1 Item1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        public ValueTuple(T1 item1)
        {
            Item1 = item1;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its field
        /// is equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1> vt &&
            comparer.Equals(Item1, vt.Item1);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1> objTuple)
                {
                    return Comparer<T1>.Default.Compare(Item1, objTuple.Item1);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1> other)
        {
            return Comparer<T1>.Default.Compare(Item1, other.Item1);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1> objTuple)
                {
                    return comparer.Compare(Item1, objTuple.Item1);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return Item1?.GetHashCode() ?? 0;
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return comparer.GetHashCode(Item1!);
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return comparer.GetHashCode(Item1!);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1)</c>,
        /// where <c>Item1</c> represents the value of <see cref="Item1"/>. If the field is <see langword="null"/>,
        /// it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 1;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index]
        {
            get
            {
                if (index != 0)
                {
                    throw new IndexOutOfRangeException();
                }
                return Item1;
            }
        }
    }

    /// <summary>
    /// Represents a 2-tuple, or pair, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2>
        : IEquatable<ValueTuple<T1, T2>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2}"/> instance's first component.
        /// </summary>
        public T1 Item1;

        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2}"/> instance's second component.
        /// </summary>
        public T2 Item2;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        public ValueTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        ///
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2}"/> instance is equal to a specified <see cref="ValueTuple{T1, T2}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2}"/> instance is equal to a specified object based on a specified comparison method.
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <param name="comparer">An object that defines the method to use to evaluate whether the two objects are equal.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        ///
        /// <remarks>
        /// This member is an explicit interface member implementation. It can be used only when the
        ///  <see cref="ValueTuple{T1, T2}"/> instance is cast to an <see cref="IStructuralEquatable"/> interface.
        ///
        /// The <see cref="IEqualityComparer.Equals"/> implementation is called only if <c>other</c> is not <see langword="null"/>,
        ///  and if it can be successfully cast (in C#) or converted (in Visual Basic) to a <see cref="ValueTuple{T1, T2}"/>
        ///  whose components are of the same types as those of the current instance. The IStructuralEquatable.Equals(Object, IEqualityComparer) method
        ///  first passes the <see cref="Item1"/> values of the <see cref="ValueTuple{T1, T2}"/> objects to be compared to the
        ///  <see cref="IEqualityComparer.Equals"/> implementation. If this method call returns <see langword="true"/>, the method is
        ///  called again and passed the <see cref="Item2"/> values of the two <see cref="ValueTuple{T1, T2}"/> instances.
        /// </remarks>
        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            return Comparer<T2>.Default.Compare(Item2, other.Item2);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Item2, objTuple.Item2);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                    Item2?.GetHashCode() ?? 0);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            return HashCode.Combine(comparer.GetHashCode(Item1!),
                                    comparer.GetHashCode(Item2!));
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2)</c>,
        /// where <c>Item1</c> and <c>Item2</c> represent the values of the <see cref="Item1"/>
        /// and <see cref="Item2"/> fields. If either field value is <see langword="null"/>,
        /// it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ", " + Item2?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 2;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] =>
            index switch
            {
                0 => Item1,
                1 => Item2,
                _ => throw new IndexOutOfRangeException(),
            };
    }

    /// <summary>
    /// Represents a 3-tuple, or triple, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    /// <typeparam name="T3">The type of the tuple's third component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2, T3>
        : IEquatable<ValueTuple<T1, T2, T3>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3}"/> instance's first component.
        /// </summary>
        public T1 Item1;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3}"/> instance's second component.
        /// </summary>
        public T2 Item2;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3}"/> instance's third component.
        /// </summary>
        public T3 Item3;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2, T3}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        /// <param name="item3">The value of the tuple's third component.</param>
        public ValueTuple(T1 item1, T2 item2, T3 item3)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2, T3}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2, T3> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1, T2, T3}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2, T3> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2)
                && EqualityComparer<T3>.Default.Equals(Item3, other.Item3);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2, T3> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2) &&
            comparer.Equals(Item3, vt.Item3);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2, T3> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            c = Comparer<T2>.Default.Compare(Item2, other.Item2);
            if (c != 0)
                return c;

            return Comparer<T3>.Default.Compare(Item3, other.Item3);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item2, objTuple.Item2);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Item3, objTuple.Item3);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2, T3}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                    Item2?.GetHashCode() ?? 0,
                                    Item3?.GetHashCode() ?? 0);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            return HashCode.Combine(comparer.GetHashCode(Item1!),
                                    comparer.GetHashCode(Item2!),
                                    comparer.GetHashCode(Item3!));
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2, T3}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2, T3}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2, Item3)</c>.
        /// If any field value is <see langword="null"/>, it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 3;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] =>
            index switch
            {
                0 => Item1,
                1 => Item2,
                2 => Item3,
                _ => throw new IndexOutOfRangeException(),
            };
    }

    /// <summary>
    /// Represents a 4-tuple, or quadruple, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    /// <typeparam name="T3">The type of the tuple's third component.</typeparam>
    /// <typeparam name="T4">The type of the tuple's fourth component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2, T3, T4>
        : IEquatable<ValueTuple<T1, T2, T3, T4>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4}"/> instance's first component.
        /// </summary>
        public T1 Item1;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4}"/> instance's second component.
        /// </summary>
        public T2 Item2;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4}"/> instance's third component.
        /// </summary>
        public T3 Item3;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4}"/> instance's fourth component.
        /// </summary>
        public T4 Item4;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2, T3, T4}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        /// <param name="item3">The value of the tuple's third component.</param>
        /// <param name="item4">The value of the tuple's fourth component.</param>
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
            Item4 = item4;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2, T3, T4}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2, T3, T4> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1, T2, T3, T4}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2, T3, T4> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2)
                && EqualityComparer<T3>.Default.Equals(Item3, other.Item3)
                && EqualityComparer<T4>.Default.Equals(Item4, other.Item4);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2, T3, T4> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2) &&
            comparer.Equals(Item3, vt.Item3) &&
            comparer.Equals(Item4, vt.Item4);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2, T3, T4> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            c = Comparer<T2>.Default.Compare(Item2, other.Item2);
            if (c != 0)
                return c;

            c = Comparer<T3>.Default.Compare(Item3, other.Item3);
            if (c != 0)
                return c;

            return Comparer<T4>.Default.Compare(Item4, other.Item4);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item2, objTuple.Item2);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item3, objTuple.Item3);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Item4, objTuple.Item4);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2, T3, T4}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                    Item2?.GetHashCode() ?? 0,
                                    Item3?.GetHashCode() ?? 0,
                                    Item4?.GetHashCode() ?? 0);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            return HashCode.Combine(comparer.GetHashCode(Item1!),
                                    comparer.GetHashCode(Item2!),
                                    comparer.GetHashCode(Item3!),
                                    comparer.GetHashCode(Item4!));
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2, T3, T4}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2, T3, T4}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2, Item3, Item4)</c>.
        /// If any field value is <see langword="null"/>, it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 4;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] =>
            index switch
            {
                0 => Item1,
                1 => Item2,
                2 => Item3,
                3 => Item4,
                _ => throw new IndexOutOfRangeException(),
            };
    }

    /// <summary>
    /// Represents a 5-tuple, or quintuple, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    /// <typeparam name="T3">The type of the tuple's third component.</typeparam>
    /// <typeparam name="T4">The type of the tuple's fourth component.</typeparam>
    /// <typeparam name="T5">The type of the tuple's fifth component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2, T3, T4, T5>
        : IEquatable<ValueTuple<T1, T2, T3, T4, T5>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4, T5>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance's first component.
        /// </summary>
        public T1 Item1;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance's second component.
        /// </summary>
        public T2 Item2;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance's third component.
        /// </summary>
        public T3 Item3;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance's fourth component.
        /// </summary>
        public T4 Item4;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance's fifth component.
        /// </summary>
        public T5 Item5;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        /// <param name="item3">The value of the tuple's third component.</param>
        /// <param name="item4">The value of the tuple's fourth component.</param>
        /// <param name="item5">The value of the tuple's fifth component.</param>
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
            Item4 = item4;
            Item5 = item5;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2, T3, T4, T5> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1, T2, T3, T4, T5}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2, T3, T4, T5> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2)
                && EqualityComparer<T3>.Default.Equals(Item3, other.Item3)
                && EqualityComparer<T4>.Default.Equals(Item4, other.Item4)
                && EqualityComparer<T5>.Default.Equals(Item5, other.Item5);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2, T3, T4, T5> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2) &&
            comparer.Equals(Item3, vt.Item3) &&
            comparer.Equals(Item5, vt.Item5);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2, T3, T4, T5> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            c = Comparer<T2>.Default.Compare(Item2, other.Item2);
            if (c != 0)
                return c;

            c = Comparer<T3>.Default.Compare(Item3, other.Item3);
            if (c != 0)
                return c;

            c = Comparer<T4>.Default.Compare(Item4, other.Item4);
            if (c != 0)
                return c;

            return Comparer<T5>.Default.Compare(Item5, other.Item5);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item2, objTuple.Item2);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item3, objTuple.Item3);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item4, objTuple.Item4);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Item5, objTuple.Item5);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                    Item2?.GetHashCode() ?? 0,
                                    Item3?.GetHashCode() ?? 0,
                                    Item4?.GetHashCode() ?? 0,
                                    Item5?.GetHashCode() ?? 0);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            return HashCode.Combine(comparer.GetHashCode(Item1!),
                                    comparer.GetHashCode(Item2!),
                                    comparer.GetHashCode(Item3!),
                                    comparer.GetHashCode(Item4!),
                                    comparer.GetHashCode(Item5!));
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2, T3, T4, T5}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2, Item3, Item4, Item5)</c>.
        /// If any field value is <see langword="null"/>, it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 5;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] =>
            index switch
            {
                0 => Item1,
                1 => Item2,
                2 => Item3,
                3 => Item4,
                4 => Item5,
                _ => throw new IndexOutOfRangeException(),
            };
    }

    /// <summary>
    /// Represents a 6-tuple, or sixtuple, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    /// <typeparam name="T3">The type of the tuple's third component.</typeparam>
    /// <typeparam name="T4">The type of the tuple's fourth component.</typeparam>
    /// <typeparam name="T5">The type of the tuple's fifth component.</typeparam>
    /// <typeparam name="T6">The type of the tuple's sixth component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2, T3, T4, T5, T6>
        : IEquatable<ValueTuple<T1, T2, T3, T4, T5, T6>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4, T5, T6>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance's first component.
        /// </summary>
        public T1 Item1;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance's second component.
        /// </summary>
        public T2 Item2;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance's third component.
        /// </summary>
        public T3 Item3;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance's fourth component.
        /// </summary>
        public T4 Item4;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance's fifth component.
        /// </summary>
        public T5 Item5;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance's sixth component.
        /// </summary>
        public T6 Item6;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        /// <param name="item3">The value of the tuple's third component.</param>
        /// <param name="item4">The value of the tuple's fourth component.</param>
        /// <param name="item5">The value of the tuple's fifth component.</param>
        /// <param name="item6">The value of the tuple's sixth component.</param>
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
            Item4 = item4;
            Item5 = item5;
            Item6 = item6;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2, T3, T4, T5, T6> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2, T3, T4, T5, T6> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2)
                && EqualityComparer<T3>.Default.Equals(Item3, other.Item3)
                && EqualityComparer<T4>.Default.Equals(Item4, other.Item4)
                && EqualityComparer<T5>.Default.Equals(Item5, other.Item5)
                && EqualityComparer<T6>.Default.Equals(Item6, other.Item6);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2, T3, T4, T5, T6> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2) &&
            comparer.Equals(Item3, vt.Item3) &&
            comparer.Equals(Item5, vt.Item5) &&
            comparer.Equals(Item6, vt.Item6);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5, T6> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2, T3, T4, T5, T6> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            c = Comparer<T2>.Default.Compare(Item2, other.Item2);
            if (c != 0)
                return c;

            c = Comparer<T3>.Default.Compare(Item3, other.Item3);
            if (c != 0)
                return c;

            c = Comparer<T4>.Default.Compare(Item4, other.Item4);
            if (c != 0)
                return c;

            c = Comparer<T5>.Default.Compare(Item5, other.Item5);
            if (c != 0)
                return c;

            return Comparer<T6>.Default.Compare(Item6, other.Item6);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5, T6> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item2, objTuple.Item2);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item3, objTuple.Item3);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item4, objTuple.Item4);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item5, objTuple.Item5);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Item6, objTuple.Item6);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                    Item2?.GetHashCode() ?? 0,
                                    Item3?.GetHashCode() ?? 0,
                                    Item4?.GetHashCode() ?? 0,
                                    Item5?.GetHashCode() ?? 0,
                                    Item6?.GetHashCode() ?? 0);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            return HashCode.Combine(comparer.GetHashCode(Item1!),
                                    comparer.GetHashCode(Item2!),
                                    comparer.GetHashCode(Item3!),
                                    comparer.GetHashCode(Item4!),
                                    comparer.GetHashCode(Item5!),
                                    comparer.GetHashCode(Item6!));
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2, T3, T4, T5, T6}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2, Item3, Item4, Item5, Item6)</c>.
        /// If any field value is <see langword="null"/>, it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 6;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] =>
            index switch
            {
                0 => Item1,
                1 => Item2,
                2 => Item3,
                3 => Item4,
                4 => Item5,
                5 => Item6,
                _ => throw new IndexOutOfRangeException(),
            };
    }

    /// <summary>
    /// Represents a 7-tuple, or sentuple, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    /// <typeparam name="T3">The type of the tuple's third component.</typeparam>
    /// <typeparam name="T4">The type of the tuple's fourth component.</typeparam>
    /// <typeparam name="T5">The type of the tuple's fifth component.</typeparam>
    /// <typeparam name="T6">The type of the tuple's sixth component.</typeparam>
    /// <typeparam name="T7">The type of the tuple's seventh component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2, T3, T4, T5, T6, T7>
        : IEquatable<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>, IValueTupleInternal, ITuple
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's first component.
        /// </summary>
        public T1 Item1;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's second component.
        /// </summary>
        public T2 Item2;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's third component.
        /// </summary>
        public T3 Item3;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's fourth component.
        /// </summary>
        public T4 Item4;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's fifth component.
        /// </summary>
        public T5 Item5;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's sixth component.
        /// </summary>
        public T6 Item6;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance's seventh component.
        /// </summary>
        public T7 Item7;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        /// <param name="item3">The value of the tuple's third component.</param>
        /// <param name="item4">The value of the tuple's fourth component.</param>
        /// <param name="item5">The value of the tuple's fifth component.</param>
        /// <param name="item6">The value of the tuple's sixth component.</param>
        /// <param name="item7">The value of the tuple's seventh component.</param>
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
            Item4 = item4;
            Item5 = item5;
            Item6 = item6;
            Item7 = item7;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2, T3, T4, T5, T6, T7> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2)
                && EqualityComparer<T3>.Default.Equals(Item3, other.Item3)
                && EqualityComparer<T4>.Default.Equals(Item4, other.Item4)
                && EqualityComparer<T5>.Default.Equals(Item5, other.Item5)
                && EqualityComparer<T6>.Default.Equals(Item6, other.Item6)
                && EqualityComparer<T7>.Default.Equals(Item7, other.Item7);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2, T3, T4, T5, T6, T7> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2) &&
            comparer.Equals(Item3, vt.Item3) &&
            comparer.Equals(Item5, vt.Item5) &&
            comparer.Equals(Item6, vt.Item6) &&
            comparer.Equals(Item7, vt.Item7);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5, T6, T7> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2, T3, T4, T5, T6, T7> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            c = Comparer<T2>.Default.Compare(Item2, other.Item2);
            if (c != 0)
                return c;

            c = Comparer<T3>.Default.Compare(Item3, other.Item3);
            if (c != 0)
                return c;

            c = Comparer<T4>.Default.Compare(Item4, other.Item4);
            if (c != 0)
                return c;

            c = Comparer<T5>.Default.Compare(Item5, other.Item5);
            if (c != 0)
                return c;

            c = Comparer<T6>.Default.Compare(Item6, other.Item6);
            if (c != 0)
                return c;

            return Comparer<T7>.Default.Compare(Item7, other.Item7);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5, T6, T7> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item2, objTuple.Item2);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item3, objTuple.Item3);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item4, objTuple.Item4);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item5, objTuple.Item5);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item6, objTuple.Item6);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Item7, objTuple.Item7);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                    Item2?.GetHashCode() ?? 0,
                                    Item3?.GetHashCode() ?? 0,
                                    Item4?.GetHashCode() ?? 0,
                                    Item5?.GetHashCode() ?? 0,
                                    Item6?.GetHashCode() ?? 0,
                                    Item7?.GetHashCode() ?? 0);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            return HashCode.Combine(comparer.GetHashCode(Item1!),
                                    comparer.GetHashCode(Item2!),
                                    comparer.GetHashCode(Item3!),
                                    comparer.GetHashCode(Item4!),
                                    comparer.GetHashCode(Item5!),
                                    comparer.GetHashCode(Item6!),
                                    comparer.GetHashCode(Item7!));
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2, Item3, Item4, Item5, Item6, Item7)</c>.
        /// If any field value is <see langword="null"/>, it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ", " + Item7?.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ", " + Item7?.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => 7;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index] =>
            index switch
            {
                0 => Item1,
                1 => Item2,
                2 => Item3,
                3 => Item4,
                4 => Item5,
                5 => Item6,
                6 => Item7,
                _ => throw new IndexOutOfRangeException(),
            };
    }

    /// <summary>
    /// Represents an 8-tuple, or octuple, as a value type.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    /// <typeparam name="T3">The type of the tuple's third component.</typeparam>
    /// <typeparam name="T4">The type of the tuple's fourth component.</typeparam>
    /// <typeparam name="T5">The type of the tuple's fifth component.</typeparam>
    /// <typeparam name="T6">The type of the tuple's sixth component.</typeparam>
    /// <typeparam name="T7">The type of the tuple's seventh component.</typeparam>
    /// <typeparam name="TRest">The type of the tuple's eighth component.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>
    : IEquatable<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>, IValueTupleInternal, ITuple
    where TRest : struct
    {
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's first component.
        /// </summary>
        public T1 Item1;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's second component.
        /// </summary>
        public T2 Item2;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's third component.
        /// </summary>
        public T3 Item3;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's fourth component.
        /// </summary>
        public T4 Item4;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's fifth component.
        /// </summary>
        public T5 Item5;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's sixth component.
        /// </summary>
        public T6 Item6;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's seventh component.
        /// </summary>
        public T7 Item7;
        /// <summary>
        /// The current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance's eighth component.
        /// </summary>
        public TRest Rest;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> value type.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        /// <param name="item3">The value of the tuple's third component.</param>
        /// <param name="item4">The value of the tuple's fourth component.</param>
        /// <param name="item5">The value of the tuple's fifth component.</param>
        /// <param name="item6">The value of the tuple's sixth component.</param>
        /// <param name="item7">The value of the tuple's seventh component.</param>
        /// <param name="rest">The value of the tuple's eight component.</param>
        public ValueTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, TRest rest)
        {
            if (rest is not IValueTupleInternal)
            {
                throw new ArgumentException("Last argument is not a ValueTuple");
            }

            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
            Item4 = item4;
            Item5 = item5;
            Item6 = item6;
            Item7 = item7;
            Rest = rest;
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
        /// <list type="bullet">
        ///     <item><description>It is a <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> value type.</description></item>
        ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
        ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
        /// </list>
        /// </remarks>
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> tuple && Equals(tuple);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/>
        /// instance is equal to a specified <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/>.
        /// </summary>
        /// <param name="other">The tuple to compare with this instance.</param>
        /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
        /// are equal to that of the current instance, using the default comparer for that field's type.
        /// </remarks>
        public bool Equals(ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> other)
        {
            return EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
                && EqualityComparer<T2>.Default.Equals(Item2, other.Item2)
                && EqualityComparer<T3>.Default.Equals(Item3, other.Item3)
                && EqualityComparer<T4>.Default.Equals(Item4, other.Item4)
                && EqualityComparer<T5>.Default.Equals(Item5, other.Item5)
                && EqualityComparer<T6>.Default.Equals(Item6, other.Item6)
                && EqualityComparer<T7>.Default.Equals(Item7, other.Item7)
                && EqualityComparer<TRest>.Default.Equals(Rest, other.Rest);
        }

        bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
            other is ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> vt &&
            comparer.Equals(Item1, vt.Item1) &&
            comparer.Equals(Item2, vt.Item2) &&
            comparer.Equals(Item3, vt.Item3) &&
            comparer.Equals(Item5, vt.Item5) &&
            comparer.Equals(Item6, vt.Item6) &&
            comparer.Equals(Item7, vt.Item7) &&
            comparer.Equals(Rest, vt.Rest);

        int IComparable.CompareTo(object? other)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> objTuple)
                {
                    return CompareTo(objTuple);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
        /// <param name="other">An instance to compare.</param>
        /// <returns>
        /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
        /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
        /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
        /// than <paramref name="other"/>.
        /// </returns>
        public int CompareTo(ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> other)
        {
            int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
            if (c != 0)
                return c;

            c = Comparer<T2>.Default.Compare(Item2, other.Item2);
            if (c != 0)
                return c;

            c = Comparer<T3>.Default.Compare(Item3, other.Item3);
            if (c != 0)
                return c;

            c = Comparer<T4>.Default.Compare(Item4, other.Item4);
            if (c != 0)
                return c;

            c = Comparer<T5>.Default.Compare(Item5, other.Item5);
            if (c != 0)
                return c;

            c = Comparer<T6>.Default.Compare(Item6, other.Item6);
            if (c != 0)
                return c;

            c = Comparer<T7>.Default.Compare(Item7, other.Item7);
            if (c != 0)
                return c;

            return Comparer<TRest>.Default.Compare(Rest, other.Rest);
        }

        int IStructuralComparable.CompareTo(object? other, IComparer comparer)
        {
            if (other is not null)
            {
                if (other is ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> objTuple)
                {
                    int c = comparer.Compare(Item1, objTuple.Item1);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item2, objTuple.Item2);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item3, objTuple.Item3);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item4, objTuple.Item4);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item5, objTuple.Item5);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item6, objTuple.Item6);
                    if (c != 0)
                        return c;

                    c = comparer.Compare(Item7, objTuple.Item7);
                    if (c != 0)
                        return c;

                    return comparer.Compare(Rest, objTuple.Rest);
                }

                ThrowHelper.ThrowArgumentException_TupleIncorrectType(this);
            }

            return 1;
        }

        /// <summary>
        /// Returns the hash code for the current <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            // We want to have a limited hash in this case. We'll use the first 7 elements of the tuple
            if (Rest is not IValueTupleInternal)
            {
                return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                        Item2?.GetHashCode() ?? 0,
                                        Item3?.GetHashCode() ?? 0,
                                        Item4?.GetHashCode() ?? 0,
                                        Item5?.GetHashCode() ?? 0,
                                        Item6?.GetHashCode() ?? 0,
                                        Item7?.GetHashCode() ?? 0);
            }

            int size = ((IValueTupleInternal)Rest).Length;
            int restHashCode = Rest.GetHashCode();
            if (size >= 8)
            {
                return restHashCode;
            }

            // In this case, the rest member has less than 8 elements so we need to combine some of our elements with the elements in rest
            int k = 8 - size;
            switch (k)
            {
                case 1:
                    return HashCode.Combine(Item7?.GetHashCode() ?? 0,
                                            restHashCode);
                case 2:
                    return HashCode.Combine(Item6?.GetHashCode() ?? 0,
                                            Item7?.GetHashCode() ?? 0,
                                            restHashCode);
                case 3:
                    return HashCode.Combine(Item5?.GetHashCode() ?? 0,
                                            Item6?.GetHashCode() ?? 0,
                                            Item7?.GetHashCode() ?? 0,
                                            restHashCode);
                case 4:
                    return HashCode.Combine(Item4?.GetHashCode() ?? 0,
                                            Item5?.GetHashCode() ?? 0,
                                            Item6?.GetHashCode() ?? 0,
                                            Item7?.GetHashCode() ?? 0,
                                            restHashCode);
                case 5:
                    return HashCode.Combine(Item3?.GetHashCode() ?? 0,
                                            Item4?.GetHashCode() ?? 0,
                                            Item5?.GetHashCode() ?? 0,
                                            Item6?.GetHashCode() ?? 0,
                                            Item7?.GetHashCode() ?? 0,
                                            restHashCode);
                case 6:
                    return HashCode.Combine(Item2?.GetHashCode() ?? 0,
                                            Item3?.GetHashCode() ?? 0,
                                            Item4?.GetHashCode() ?? 0,
                                            Item5?.GetHashCode() ?? 0,
                                            Item6?.GetHashCode() ?? 0,
                                            Item7?.GetHashCode() ?? 0,
                                            restHashCode);
                case 7:
                case 8:
                    return HashCode.Combine(Item1?.GetHashCode() ?? 0,
                                            Item2?.GetHashCode() ?? 0,
                                            Item3?.GetHashCode() ?? 0,
                                            Item4?.GetHashCode() ?? 0,
                                            Item5?.GetHashCode() ?? 0,
                                            Item6?.GetHashCode() ?? 0,
                                            Item7?.GetHashCode() ?? 0,
                                            restHashCode);
            }

            Debug.Fail("Missed all cases for computing ValueTuple hash code");
            return -1;
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        private int GetHashCodeCore(IEqualityComparer comparer)
        {
            // We want to have a limited hash in this case. We'll use the first 7 elements of the tuple
            if (Rest is not IValueTupleInternal rest)
            {
                return HashCode.Combine(comparer.GetHashCode(Item1!), comparer.GetHashCode(Item2!), comparer.GetHashCode(Item3!),
                                        comparer.GetHashCode(Item4!), comparer.GetHashCode(Item5!), comparer.GetHashCode(Item6!),
                                        comparer.GetHashCode(Item7!));
            }

            int size = rest.Length;
            int restHashCode = rest.GetHashCode(comparer);
            if (size >= 8)
            {
                return restHashCode;
            }

            // In this case, the rest member has less than 8 elements so we need to combine some our elements with the elements in rest
            int k = 8 - size;
            switch (k)
            {
                case 1:
                    return HashCode.Combine(comparer.GetHashCode(Item7!),
                                            restHashCode);
                case 2:
                    return HashCode.Combine(comparer.GetHashCode(Item6!),
                                            comparer.GetHashCode(Item7!),
                                            restHashCode);
                case 3:
                    return HashCode.Combine(comparer.GetHashCode(Item5!),
                                            comparer.GetHashCode(Item6!),
                                            comparer.GetHashCode(Item7!),
                                            restHashCode);
                case 4:
                    return HashCode.Combine(comparer.GetHashCode(Item4!),
                                            comparer.GetHashCode(Item5!),
                                            comparer.GetHashCode(Item6!),
                                            comparer.GetHashCode(Item7!),
                                            restHashCode);
                case 5:
                    return HashCode.Combine(comparer.GetHashCode(Item3!),
                                            comparer.GetHashCode(Item4!),
                                            comparer.GetHashCode(Item5!),
                                            comparer.GetHashCode(Item6!),
                                            comparer.GetHashCode(Item7!),
                                            restHashCode);
                case 6:
                    return HashCode.Combine(comparer.GetHashCode(Item2!),
                                            comparer.GetHashCode(Item3!),
                                            comparer.GetHashCode(Item4!),
                                            comparer.GetHashCode(Item5!),
                                            comparer.GetHashCode(Item6!),
                                            comparer.GetHashCode(Item7!),
                                            restHashCode);
                case 7:
                case 8:
                    return HashCode.Combine(comparer.GetHashCode(Item1!),
                                            comparer.GetHashCode(Item2!),
                                            comparer.GetHashCode(Item3!),
                                            comparer.GetHashCode(Item4!),
                                            comparer.GetHashCode(Item5!),
                                            comparer.GetHashCode(Item6!),
                                            comparer.GetHashCode(Item7!),
                                            restHashCode);
            }

            Debug.Fail("Missed all cases for computing ValueTuple hash code");
            return -1;
        }

        int IValueTupleInternal.GetHashCode(IEqualityComparer comparer)
        {
            return GetHashCodeCore(comparer);
        }

        /// <summary>
        /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance.
        /// </summary>
        /// <returns>The string representation of this <see cref="ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}"/> instance.</returns>
        /// <remarks>
        /// The string returned by this method takes the form <c>(Item1, Item2, Item3, Item4, Item5, Item6, Item7, Rest)</c>.
        /// If any field value is <see langword="null"/>, it is represented as <see cref="string.Empty"/>.
        /// </remarks>
        public override string ToString()
        {
            if (Rest is IValueTupleInternal)
            {
                return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ", " + Item7?.ToString() + ", " + ((IValueTupleInternal)Rest).ToStringEnd();
            }

            return "(" + Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ", " + Item7?.ToString() + ", " + Rest.ToString() + ")";
        }

        string IValueTupleInternal.ToStringEnd()
        {
            if (Rest is IValueTupleInternal)
            {
                return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ", " + Item7?.ToString() + ", " + ((IValueTupleInternal)Rest).ToStringEnd();
            }

            return Item1?.ToString() + ", " + Item2?.ToString() + ", " + Item3?.ToString() + ", " + Item4?.ToString() + ", " + Item5?.ToString() + ", " + Item6?.ToString() + ", " + Item7?.ToString() + ", " + Rest.ToString() + ")";
        }

        /// <summary>
        /// The number of positions in this data structure.
        /// </summary>
        int ITuple.Length => Rest is IValueTupleInternal ? 7 + ((IValueTupleInternal)Rest).Length : 8;

        /// <summary>
        /// Get the element at position <param name="index"/>.
        /// </summary>
        object? ITuple.this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return Item1;
                    case 1:
                        return Item2;
                    case 2:
                        return Item3;
                    case 3:
                        return Item4;
                    case 4:
                        return Item5;
                    case 5:
                        return Item6;
                    case 6:
                        return Item7;
                }

                if (Rest is IValueTupleInternal)
                {
                    return ((IValueTupleInternal)Rest)[index - 7];
                }


                if (index == 7)
                {
                    return Rest;
                }

                throw new IndexOutOfRangeException();
            }
        }
    }
}