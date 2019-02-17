using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

namespace Samples.Common
{
    [DisableAutoCreation]
    [UpdateAfter(typeof(RandomInitialHeadingSystem))]
    public class RandomInitialHeadingBarrier : BarrierSystem
    { }

    /// <summary>
    /// Каждый кадр фильтруем, если на рыбешке есть RandomInitialHeading и Heading - удаляем тег RandomInitialHeading, присваеваем ему случайное направление
    /// 
    /// </summary>
    public class RandomInitialHeadingSystem : JobComponentSystem
    {
        private RandomInitialHeadingBarrier m_Barrier; //окей, барььер с аттрибутами обновления после этой системы, и (вроде) не создающаяся автоматически

        protected override void OnCreateManager()
        {
            m_Barrier = World.Active.GetOrCreateManager<RandomInitialHeadingBarrier>();
            //ну раз автоматически мы его не создаем, мы создаем его вручную. Используем для этого другой менеджер
        }

        /// <summary>
        /// Heading и RandomInitialHeading есть только у рыбешек, на акул и ведущих рыб система не влияет
        /// </summary>
        struct SetInitialHeadingJob : IJobProcessComponentDataWithEntity<RandomInitialHeading, Heading>
        {
            public EntityCommandBuffer.Concurrent Commands;
            public Unity.Mathematics.Random Random;

            /// <summary>
            /// если честь, что entity - индекс, то нахрен нужен index? 
            /// </summary>
            /// <param name="entity"></param>
            /// <param name="index"></param>
            /// <param name="randomInitialHeading"></param>
            /// <param name="heading"></param>
            public void Execute(Entity entity, int index, [ReadOnly] ref RandomInitialHeading randomInitialHeading, ref Heading heading)
            {
                heading = new Heading
                {
                    Value = Random.NextFloat3Direction()
                };

                Commands.RemoveComponent<RandomInitialHeading>(index, entity);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var job = new SetInitialHeadingJob
            {
                Commands = m_Barrier.CreateCommandBuffer().ToConcurrent(),
                Random = new Random(0xabcdef)
            };
            var handle = job.Schedule(this, inputDeps);
            ///воо, теперь вдуплять начинаю
            ///каждый кадр мы берем Job, говорим ей "к выполнению, что нужно сама возьми", Job при этом сначала собирает Enteties из EntetiesManager по фильтру, потом вызывает Execute

            m_Barrier.AddJobHandleForProducer(handle);//эта штука как-то связывает JobSystem и BarrierSystem
            //TODO: разобраться с продюссером.
            return handle;
        }
    }
}
