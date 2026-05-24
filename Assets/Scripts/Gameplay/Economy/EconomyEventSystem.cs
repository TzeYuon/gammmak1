using System;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(PostUpdateSystemGroup))]
public partial class EconomyEventSystem : SystemBase
{
    public static event Action<float> OnBiomassChanged;
    private float lastBiomassAmount = -1f;

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerEconomy>();
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.TryGetSingleton<PlayerEconomy>(out var economy))
        {
            if (economy.TotalBiomass != lastBiomassAmount)
            {
                lastBiomassAmount = economy.TotalBiomass;
                OnBiomassChanged?.Invoke(economy.TotalBiomass);
            }
        }
    }
}
