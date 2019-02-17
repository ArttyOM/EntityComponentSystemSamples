using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

namespace Samples.Common
{
    /// <summary>
    /// Heading-компонент есть только у рыбешек
    /// Другие системы меняют направление heading, а эта выставляет конкретный rotation, такой, чтобы рыбешка всегда ровно плавала
    /// </summary>
    public class HeadingSystem : JobComponentSystem
    {
        /// <summary>
        /// Каждый кадр делает ничего - это с виду так.
        /// Другие системы меняют направление heading, а эта выставляет конкретный rotation, такой, чтобы рыбешка всегда ровно плавала
        /// </summary>
        [BurstCompile]
        struct RotationFromHeading : IJobProcessComponentData<Heading, Rotation>
        {
            public void Execute([ReadOnly] ref Heading heading, ref Rotation rotation)
            {
                var rotationFromHeading = quaternion.LookRotationSafe(heading.Value, math.up());
                rotation = new Rotation { Value = rotationFromHeading };
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var rotationFromHeadingJob = new RotationFromHeading();
            var rotationFromHeadingJobHandle = rotationFromHeadingJob.Schedule(this, inputDeps);

            return rotationFromHeadingJobHandle;
        }
    }
}
