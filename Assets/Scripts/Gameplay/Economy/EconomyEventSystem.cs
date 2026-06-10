using System;
using Unity.Entities;

[UpdateInGroup(typeof(LateGameplayGroup))]
public partial class EconomyEventSystem : SystemBase
{
    public static event Action<float> OnBiomassChanged;
    private float lastBiomassAmount = -1f;

    private EntityQuery economyQuery;

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerEconomy>();
        economyQuery = SystemAPI.QueryBuilder().WithAll<PlayerEconomy>().Build();
    }

    protected override void OnUpdate()
    {
        economyQuery.CompleteDependency();
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
