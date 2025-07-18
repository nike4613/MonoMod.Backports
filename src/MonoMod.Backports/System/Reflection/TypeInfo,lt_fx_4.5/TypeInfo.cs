﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Reflection
{
    public abstract partial class TypeInfo : Type, IReflectableType
    {
        // this ctor is only available in Core3.0 and NS2.1 :/
        private protected TypeInfo() { }

        TypeInfo IReflectableType.GetTypeInfo() => this;
        public virtual Type AsType() => this;

        // this is what's done by the BCL
#pragma warning disable CA1819 // Properties should not return arrays
        public virtual Type[] GenericTypeParameters => IsGenericTypeDefinition ? GetGenericArguments() : Type.EmptyTypes;
#pragma warning restore CA1819 // Properties should not return arrays

        public virtual EventInfo? GetDeclaredEvent(string name) => GetEvent(name, TypeInfo.DeclaredOnlyLookup);
        public virtual FieldInfo? GetDeclaredField(string name) => GetField(name, TypeInfo.DeclaredOnlyLookup);
        public virtual MethodInfo? GetDeclaredMethod(string name) => GetMethod(name, TypeInfo.DeclaredOnlyLookup);
        public virtual TypeInfo? GetDeclaredNestedType(string name) => GetNestedType(name, TypeInfo.DeclaredOnlyLookup)?.GetTypeInfo();
        public virtual PropertyInfo? GetDeclaredProperty(string name) => GetProperty(name, TypeInfo.DeclaredOnlyLookup);

        public abstract bool IsConstructedGenericType { get; }

        public virtual IEnumerable<MethodInfo> GetDeclaredMethods(string name)
        {
            foreach (MethodInfo method in GetMethods(TypeInfo.DeclaredOnlyLookup))
            {
                if (method.Name == name)
                    yield return method;
            }
        }

        public virtual IEnumerable<ConstructorInfo> DeclaredConstructors => GetConstructors(TypeInfo.DeclaredOnlyLookup);
        public virtual IEnumerable<EventInfo> DeclaredEvents => GetEvents(TypeInfo.DeclaredOnlyLookup);
        public virtual IEnumerable<FieldInfo> DeclaredFields => GetFields(TypeInfo.DeclaredOnlyLookup);
        public virtual IEnumerable<MemberInfo> DeclaredMembers => GetMembers(TypeInfo.DeclaredOnlyLookup);
        public virtual IEnumerable<MethodInfo> DeclaredMethods => GetMethods(TypeInfo.DeclaredOnlyLookup);
        public virtual IEnumerable<System.Reflection.TypeInfo> DeclaredNestedTypes
        {
            get
            {
                foreach (Type t in GetNestedTypes(TypeInfo.DeclaredOnlyLookup))
                {
                    yield return t.GetTypeInfo();
                }
            }
        }
        public virtual IEnumerable<PropertyInfo> DeclaredProperties => GetProperties(TypeInfo.DeclaredOnlyLookup);

        public virtual IEnumerable<Type> ImplementedInterfaces => GetInterfaces();

        //a re-implementation of ISAF from Type, skipping the use of UnderlyingType
        public virtual bool IsAssignableFrom(TypeInfo typeInfo)
        {
            if (typeInfo == null)
                return false;

            if (this == typeInfo)
                return true;

            // If c is a subclass of this class, then c can be cast to this type.
            if (typeInfo.IsSubclassOf(this))
                return true;

            if (IsInterface)
            {
                return ImplementsInterface(typeInfo, this);
            }
            else if (IsGenericParameter)
            {
                Type[] constraints = GetGenericParameterConstraints();
                for (int i = 0; i < constraints.Length; i++)
                    if (!constraints[i].IsAssignableFrom(typeInfo))
                        return false;

                return true;
            }

            return false;
        }

        private static bool ImplementsInterface(Type self, Type ifaceType)
        {
            Type? t = self;
            while (t != null)
            {
                Type[] interfaces = t.GetInterfaces();
                if (interfaces != null)
                {
                    for (int i = 0; i < interfaces.Length; i++)
                    {
                        // Interfaces don't derive from other interfaces, they implement them.
                        // So instead of IsSubclassOf, we should use ImplementInterface instead.
                        if (interfaces[i] == ifaceType ||
                            (interfaces[i] != null && ImplementsInterface(interfaces[i], ifaceType)))
                            return true;
                    }
                }

                t = t.BaseType;
            }

            return false;
        }

        private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    }
}