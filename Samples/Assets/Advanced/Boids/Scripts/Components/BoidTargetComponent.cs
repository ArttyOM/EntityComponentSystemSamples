using System;
using Unity.Entities;

/// <summary>
/// Компонент ведущей рыбы.
/// Просто тег.
/// </summary>
[Serializable]
public struct BoidTarget : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class BoidTargetComponent : ComponentDataWrapper<BoidTarget> { }
