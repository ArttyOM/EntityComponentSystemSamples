using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Компонент направления взгляда рыбешки
/// Под капотом:
/// float3 вектор направления
/// конструктор (float3)
/// </summary>
[Serializable]
public struct Heading : IComponentData
{
    public float3 Value;

    public Heading(float3 heading)
    {
        Value = heading;
    }
}

[UnityEngine.DisallowMultipleComponent]
public class HeadingComponent : ComponentDataWrapper<Heading> { }
