using System;
using Unity.Entities;

/// <summary>
/// Компонент, отвечающий за случайное начальное направление взгляда
/// Под капотом: Просто тег.
/// </summary>
[Serializable]
public struct RandomInitialHeading : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class RandomInitialHeadingComponent : ComponentDataWrapper<RandomInitialHeading> { }
