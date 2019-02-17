using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Samples.Common
{
    /// <summary>
    /// У Акул и ВедущихРыб нет компонента MoveSpeed, они анимацией перемещаются.
    /// У ВедомыхРыб (далее буду писать "рыбешек" - есть этот компонент, а также PositionComponent и RotationComponent
    /// Система двигает рыбешку вперед относительно себя
    /// </summary>
    public class MoveForwardSystem : JobComponentSystem
    {
        /// <summary>
        /// берем рыбешку, по её компоненту поворота вычисляем вектор направления, умножаем на скорость (и dt не забываем) - добавляем это значение к позиции
        /// В итоге рыбешка каждый кадр перемещается вперед
        /// </summary>
        [BurstCompile]
        struct MoveForwardRotation : IJobProcessComponentData<Position, Rotation, MoveSpeed>
        {
            public float dt;
        
            public void Execute(ref Position position, [ReadOnly] ref Rotation rotation, [ReadOnly] ref MoveSpeed speed)
            {
                position = new Position
                {
                    Value = position.Value + (dt * speed.speed * math.forward(rotation.Value))
                };
            }
        }

        /// <summary>
        /// как же мне не нравится OnUpdate
        /// TODO: разобраться, какие еще JobHandle предлагает Юнити Жоб
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns></returns>
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var moveForwardRotationJob = new MoveForwardRotation
            {
                dt = Time.deltaTime
            };
            var moveForwardRotationJobHandle = moveForwardRotationJob.Schedule(this, inputDeps);
            return moveForwardRotationJobHandle;
        }
    }
}
