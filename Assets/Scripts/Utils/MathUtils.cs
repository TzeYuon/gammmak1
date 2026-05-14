using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;
using System.Runtime.CompilerServices;

public static class NativeArrayExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void InterlockedAdd(this NativeArray<int> array, int index, int valueToAdd)
    {
        int* ptr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
        Interlocked.Add(ref ptr[index], valueToAdd);
    }
}