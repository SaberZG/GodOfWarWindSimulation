using System;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections
{
    public static class NativeArrayRefExtension
    {
        public static ref T GetRef<T>(this NativeArray<T> array, int index) where T : struct
        {
            if (index < 0 || index >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            unsafe
            {
                return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
            }
        }
    }
}