using System;
using Unity.Entities;

/// <summary>
/// Компонент - препятствие для рыбешек
/// в конкретном случае - акула
/// Просто тег.
/// </summary>
[Serializable]
public struct BoidObstacle : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
