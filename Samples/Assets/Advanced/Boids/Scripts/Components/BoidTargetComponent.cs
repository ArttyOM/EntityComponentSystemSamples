using System;
using Unity.Entities;

/// <summary>
/// ��������� ������� ����.
/// ������ ���.
/// </summary>
[Serializable]
public struct BoidTarget : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class BoidTargetComponent : ComponentDataWrapper<BoidTarget> { }
