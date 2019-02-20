using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// компонент, отвечающий за генерацию рыбешек
/// Под капотом:
/// GameObject префаб-образец рыбешки - кого спавнить
/// float радиус сферы, в пределах которой происходит генерация.
/// Центр сферы - данные из компонента позиции порождающей сущности
/// int количество - сколько нужно сгенерировать
/// </summary>
[Serializable]
public struct SpawnRandomInSphere : ISharedComponentData
{
    public GameObject prefab;
    public float radius;
    public int count;
}

public class SpawnRandomInSphereComponent : SharedComponentDataWrapper<SpawnRandomInSphere> { }
