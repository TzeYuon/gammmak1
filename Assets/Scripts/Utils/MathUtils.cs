using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public static class NativeArrayExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void InterlockedAdd(this NativeArray<int> array, int index, int valueToAdd)
    {
        int* ptr = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
        Interlocked.Add(ref ptr[index], valueToAdd);
    }
}

public static class MathUtils
{
    public static List<int> GetUniqueNumbers(int seed, int amountToGet, int minInclusive, int maxExclusive)
    {
        // Safety check
        if (amountToGet > maxExclusive - minInclusive)
        {
            return null;
        }

        var rng = Unity.Mathematics.Random.CreateFromIndex(math.hash(new float2(System.DateTime.Now.Ticks, seed)));
        List<int> uniqueNumbers = new();

        while (uniqueNumbers.Count < amountToGet)
        {
            int roll = rng.NextInt(minInclusive, maxExclusive);
            if (!uniqueNumbers.Contains(roll)) uniqueNumbers.Add(roll);
        }

        return uniqueNumbers;
    }
}