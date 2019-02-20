﻿using System;
using Unity.Entities;

/// <summary>
/// Store float speed. This component requests that if another component is moving the PositionComponent
/// it should respect this value and move the position at the constant speed specified.
///
/// Компонент скорости. TODO - поглубже вникнуть в родной комментарий
/// Под капотом:
/// float величина скорости
/// </summary>
[Serializable]
public struct MoveSpeed : IComponentData
{
    public float speed;
}

[UnityEngine.DisallowMultipleComponent]
public class MoveSpeedComponent : ComponentDataWrapper<MoveSpeed> { }
