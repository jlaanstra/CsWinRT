﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using WinRT;

#pragma warning disable CS8500

namespace ABI.WinRT.Interop
{
    internal struct ComCallData
    {
        public int dwDispid;
        public int dwReserved;
        public IntPtr pUserDefined;
    }

#if NET && CsWinRT_LANG_11_FEATURES
    internal unsafe struct IContextCallbackVftbl
    {
        private global::WinRT.Interop.IUnknownVftbl IUnknownVftbl;
        private delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ComCallData*, Guid*, int, IntPtr, int> ContextCallback_4;

        public static void ContextCallback(IntPtr contextCallbackPtr, Action callback, Action onFailCallback)
        {
            ComCallData comCallData;
            comCallData.dwDispid = 0;
            comCallData.dwReserved = 0;

            // Copy the callback into a local to make sure it really is a local that
            // gets marked as address taken, rather than something that could potentially
            // be inlined into the caller into some state machine or anything else not safe.
            Action callbackAddressTaken = callback;

            // We can just store a pointer to the callback to invoke in the context,
            // so we don't need to allocate another closure or anything. The callback
            // will be kept alive automatically, because 'comCallData' is address exposed.
            // We only do this if we can use C# 11, and if we're on modern .NET, to be safe.
            // In the callback below, we can then just retrieve the Action again to invoke it.
            comCallData.pUserDefined = (IntPtr)(void*)&callbackAddressTaken;
            
            [UnmanagedCallersOnly]
            static int InvokeCallback(ComCallData* comCallData)
            {
                try
                {
                    // Dereference the pointer to Action and invoke it (see notes above).
                    // Once again, the pointer is not to the Action object, but just to the
                    // local *reference* to the object, which is pinned (as it's a local).
                    // That means that there's no pinning to worry about either.
                    ((Action*)comCallData->pUserDefined)->Invoke();

                    return 0; // S_OK
                }
                catch (Exception e)
                {
                    return e.HResult;
                }
            }

            Guid iid = InterfaceIIDs.ICallbackWithNoReentrancyToApplicationSTA_IID;

            int hresult = (*(IContextCallbackVftbl**)contextCallbackPtr)->ContextCallback_4(
                contextCallbackPtr,
                (IntPtr)(delegate* unmanaged<ComCallData*, int>)&InvokeCallback,
                &comCallData,
                &iid,
                /* iMethod */ 5,
                IntPtr.Zero);

            if (hresult < 0)
            {
                onFailCallback?.Invoke();
            }
        }
    }
#else
    internal unsafe delegate int PFNCONTEXTCALL(ComCallData* data);

    [Guid("000001da-0000-0000-C000-000000000046")]
    internal sealed unsafe class IContextCallback
    {
        internal static readonly Guid IID = InterfaceIIDs.IContextCallback_IID;

        [Guid("000001da-0000-0000-C000-000000000046")]
        public struct Vftbl
        {
            global::WinRT.Interop.IUnknownVftbl IUnknownVftbl;
            private void* _ContextCallback;
            public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ComCallData*, Guid*, int, IntPtr, int> ContextCallback_4
            {
                get => (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, ComCallData*, Guid*, int, IntPtr, int>)_ContextCallback;
                set => _ContextCallback = (void*)value;
            }
        }
        public static ObjectReference<Vftbl> FromAbi(IntPtr thisPtr) => ObjectReference<Vftbl>.FromAbi(thisPtr);

        public static implicit operator IContextCallback(IObjectReference obj) => (obj != null) ? new IContextCallback(obj) : null;
        public static implicit operator IContextCallback(ObjectReference<Vftbl> obj) => (obj != null) ? new IContextCallback(obj) : null;
        private readonly ObjectReference<Vftbl> _obj;
        [Obsolete]
        public IntPtr ThisPtr => _obj.ThisPtr;
        public ObjectReference<I> AsInterface<I>() => _obj.As<I>();
        public A As<A>() => _obj.AsType<A>();
        public IContextCallback(IObjectReference obj) : this(obj.As<Vftbl>()) { }
        public IContextCallback(ObjectReference<Vftbl> obj)
        {
            _obj = obj;
        }

        public unsafe void ContextCallback(PFNCONTEXTCALL pfnCallback, ComCallData* pParam, Guid riid, int iMethod)
        {
            bool success =  false;
            try
            {
                _obj.DangerousAddRef(ref success);
                var thisPtr = _obj.DangerousGetPtr();

                var callback = Marshal.GetFunctionPointerForDelegate(pfnCallback);
                var result = _obj.Vftbl.ContextCallback_4(thisPtr, callback, pParam, &riid, iMethod, IntPtr.Zero);
                GC.KeepAlive(pfnCallback);
                Marshal.ThrowExceptionForHR(result);
            }
            finally
            {
                if (success)
                {
                    _obj.DangerousRelease();
                }
            }
        }
    }
#endif
}