using Unity.Entities;
/// <summary>
/// Компонент, отвечающий за движение вперед
/// Под капотом: просто тег.
/// </summary>
public struct MoveForward : ISharedComponentData { }

public class MoveForwardComponent : SharedComponentDataWrapper<MoveForward> { }
