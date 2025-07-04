﻿#if NETFRAMEWORK && !NET40_OR_GREATER
#define BACKPORTS_IMPL
#endif

#if BACKPORTS_IMPL || NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
#define HAS_CWT_CLEAR
#endif
#if BACKPORTS_IMPL || NET7_0_OR_GREATER
#define HAS_TRYADD
#endif

#if !BACKPORTS_IMPL && !NETSTANDARD2_1_OR_GREATER  && !NETCOREAPP2_0_OR_GREATER
#define CWT_NOT_ENUMERABLE
#endif

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
#if CWT_NOT_ENUMERABLE
using System.Reflection;
#endif

namespace System.Runtime.CompilerServices
{

    // WE use this for our implementation of CWT so that Roslyn always uses this extension method to enumerate it on old frameworks
    internal interface ICWTEnumerable<T>
    {
        IEnumerable<T> SelfEnumerable { get; }
        IEnumerator<T> GetEnumerator();
    }

    internal sealed class CWTEnumerable<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : class where TValue : class?
    {
        private readonly ConditionalWeakTable<TKey, TValue> cwt;

        public CWTEnumerable(ConditionalWeakTable<TKey, TValue> table)
            => cwt = table;

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => cwt.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class ConditionalWeakTableExtensions
    {
#if CWT_NOT_ENUMERABLE
        private static class CWTInfoHolder<TKey, TValue> where TKey : class where TValue : class?
        {
            private static readonly MethodInfo? get_KeysMethod;
            public static readonly GetKeys? get_Keys;

            public delegate IEnumerable<TKey> GetKeys(ConditionalWeakTable<TKey, TValue> cwt);

            [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline",
                Justification = "There are no methods which would get any performance impact.")]
            static CWTInfoHolder()
            {
                get_KeysMethod = typeof(ConditionalWeakTable<TKey, TValue>).GetProperty("Keys", BindingFlags.NonPublic | BindingFlags.Instance)?.GetGetMethod(nonPublic: true);
                if (get_KeysMethod is not null)
                {
                    get_Keys = (GetKeys)Delegate.CreateDelegate(typeof(GetKeys), get_KeysMethod);
                }
            }
        }
#endif

        [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code",
            Justification = "This check is expected to be always true for some targets.")]
        public static IEnumerable<KeyValuePair<TKey, TValue>> AsEnumerable<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> self) where TKey : class where TValue : class?
        {
            ThrowHelper.ThrowIfArgumentNull(self, nameof(self));
            if (self is IEnumerable<KeyValuePair<TKey, TValue>> enumerable)
            {
                return enumerable;
            }
            else if (self is ICWTEnumerable<KeyValuePair<TKey, TValue>> cwt)
            {
                return cwt.SelfEnumerable;
            }
            else
            {
                return new CWTEnumerable<TKey, TValue>(self);
            }
        }

        [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code",
            Justification = "This check is expected to be always true for some targets.")]
        public static IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> self) where TKey : class where TValue : class?
        {
            ThrowHelper.ThrowIfArgumentNull(self, nameof(self));
            if (self is IEnumerable<KeyValuePair<TKey, TValue>> enumerable)
            {
                return enumerable.GetEnumerator();
            }
            else if (self is ICWTEnumerable<KeyValuePair<TKey, TValue>> cwtEnum)
            {
                return cwtEnum.GetEnumerator();
            }
            else
            {
#if !CWT_NOT_ENUMERABLE
                throw new PlatformNotSupportedException("This version of MonoMod.Backports was built targeting a version of the framework " +
                    "where ConditionalWeakTable is enumerable, but it isn't!");
#else
                if (CWTInfoHolder<TKey, TValue>.get_Keys is { } getKeys)
                {
                    return Enumerate(self, getKeys(self));
                    static IEnumerator<KeyValuePair<TKey, TValue>> Enumerate(ConditionalWeakTable<TKey, TValue> cwt, IEnumerable<TKey> keys)
                    {
                        foreach (var key in keys)
                        {
                            if (cwt.TryGetValue(key, out TValue? value))
                            {
                                yield return new KeyValuePair<TKey, TValue>(key, value);
                            }
                        }
                    }
                }
                else
                {
                    throw new PlatformNotSupportedException("Could not get Keys property of ConditionalWeakTable to enumerate it");
                }
#endif
            }
        }

        public static void Clear<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> self) where TKey : class where TValue : class?
        {
            ThrowHelper.ThrowIfArgumentNull(self, nameof(self));
#if HAS_CWT_CLEAR
            self.Clear();
#else
            foreach (var kvp in self)
            {
                self.Remove(kvp.Key);
            }
#endif
        }

        public static bool TryAdd<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> self, TKey key, TValue value) where TKey : class where TValue : class?
        {
            ThrowHelper.ThrowIfArgumentNull(self, nameof(self));
#if HAS_TRYADD
            return self.TryAdd(key, value);
#else
            var didAdd = false;
            _ = self.GetValue(key, _ =>
            {
                didAdd = true;
                return value;
            });
            return didAdd;
#endif
        }

        // I'd like to include AddOrUpdate, but I don't think there's another way to update a value...
        // We *could* remove-then-add, but I'm not sure I'm OK with that given its concurrency implications.
        // I could do reflection again to find the lock object, but I'm not sure that its an important enough API.
    }
}
