using System;
using Unity.Entities;

/// <summary>
/// Компонент с данными рыбешки
///Под капотом:
///float радиус ячейки (TODO - разобраться подробнее)
///float вес разделения (TODO - разобраться подробнее)
///float вес выравнивания (TODO - разобраться подробнее)
///float вес цели (TODO - разобраться подробнее)
///float расстояние "антипатии" к акуле - рыбешка не реагирует на акулу, пока не та не подойдет ближе указанного расстояния
/// </summary>
[Serializable]
public struct Boid : ISharedComponentData
{
    public float cellRadius;
    public float separationWeight;
    public float alignmentWeight;
    public float targetWeight;
    public float obstacleAversionDistance;
}

public class BoidComponent : SharedComponentDataWrapper<Boid> { }
