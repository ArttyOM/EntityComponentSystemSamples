using System;
using Unity.Entities;

/// <summary>
/// Ненужный компонент
/// </summary>
[Serializable]
public struct RotationSpeed : IComponentData
{
    public float Value;
}

[UnityEngine.DisallowMultipleComponent]
public class RotationSpeedComponent : ComponentDataWrapper<RotationSpeed> { }
