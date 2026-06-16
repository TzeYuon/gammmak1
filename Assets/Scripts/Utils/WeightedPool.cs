using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WeightedItem<T>
{
    public T Item;
    public int Weight = 10;
}

[System.Serializable]
public class WeightedPool<T>
{
    private bool isInit = false;
    private int totalWeight;
    public List<WeightedItem<T>> Pool = new List<WeightedItem<T>>();

    public T GetRandom()
    {
        if (!isInit)
        {
            isInit = true;
            CalculateTotalWeight();
        }

        if (Pool.Count == 0) return default;

        int randomRoll = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var item in Pool)
        {
            currentWeight += item.Weight;
            if (randomRoll < currentWeight)
                return item.Item;
        }

        return Pool[0].Item;
    }

    void CalculateTotalWeight()
    {
        totalWeight = 0;
        foreach (var item in Pool)
        {
            totalWeight += item.Weight;
        }
    }

    public void Add(T item, int weight)
    {
        Add(new WeightedItem<T> { Item = item, Weight = weight });
    }

    public void Add(WeightedItem<T> weightedItem)
    {
        totalWeight += weightedItem.Weight;
        Pool.Add(weightedItem);
    }

    public void Remove(WeightedItem<T> weightedItem)
    {
        totalWeight -= weightedItem.Weight;
        Pool.Remove(weightedItem);
    }
}