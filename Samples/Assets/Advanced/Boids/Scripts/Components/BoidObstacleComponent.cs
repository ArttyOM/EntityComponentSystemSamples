using System;
using Unity.Entities;

/// <summary>
/// ��������� - ����������� ��� �������
/// � ���������� ������ - �����
/// ������ ���.
/// </summary>
[Serializable]
public struct BoidObstacle : IComponentData { }

[UnityEngine.DisallowMultipleComponent]
public class BoidObstacleComponent : ComponentDataWrapper<BoidObstacle> { }
