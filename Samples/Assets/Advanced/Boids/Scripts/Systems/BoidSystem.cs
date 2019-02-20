using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Samples.Common;

namespace Samples.Boids
{
    /// <summary>
    /// Самая сложная система в проекте, и сюда 99% логики засунули.
    /// </summary>
    public class BoidSystem : JobComponentSystem
    {
        //поля ComponentGroup проходят инициализацию в OnManagerStarted
        /// <summary>
        /// все рыбешки, присутствующие в игре на момент старта менеджера (TODO: или просто все рыбешки? Разобраться
        /// </summary>
        private ComponentGroup  m_BoidGroup;
        private ComponentGroup  m_TargetGroup;//коллекция компонентов "ведущих"
        private ComponentGroup  m_ObstacleGroup; //коллекция акул

        ///задел на 10 разных типов Boid - зачем оно тут?
        private List<Boid>      m_UniqueTypes = new List<Boid>(10);

        private List<PrevCells> m_PrevCells   = new List<PrevCells>();

        /// <summary>
        /// вспомогательная структура
        /// </summary>
        struct PrevCells
        {
            public NativeMultiHashMap<int, int> hashMap;
            public NativeArray<int>             cellIndices;
            public NativeArray<Position>        copyTargetPositions;
            public NativeArray<Position>        copyObstaclePositions;
            public NativeArray<Heading>         cellAlignment;
            public NativeArray<Position>        cellSeparation;
            public NativeArray<int>             cellObstaclePositionIndex;
            public NativeArray<float>           cellObstacleDistance;
            public NativeArray<int>             cellTargetPistionIndex;
            public NativeArray<int>             cellCount;
        }

        /// <summary>
        /// в этот раз использован другой тип фильтра походу - через атрибут
        /// фильтруем по компоненту рыбешек и позиции
        /// позиции из float3 переводим в int (с помощью Hash) и храним в коллекции
        ///
        /// Интересно, почему мы не кешируем позиции акул и Ведущих?
        /// </summary>
        [BurstCompile]
        [RequireComponentTag(typeof(Boid))]
        struct HashPositions : IJobProcessComponentDataWithEntity<Position>///обожаю километровые имена интерфейсов
        {
            public NativeMultiHashMap<int, int>.Concurrent hashMap;
            public float cellRadius;

            public void Execute(Entity entity, int index, [ReadOnly]ref Position position)
            {
                var hash = GridHash.Hash(position.Value, cellRadius);
                //закодированные координаты, в которых находится сущность
                hashMap.Add(hash, index);//добавляем закодированные координаты рыбешки в коллекцию
            }
        }

        [BurstCompile]
        struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices //даже имена интерфейсов страшно читать
        {
            ///массив ID клеток
            public NativeArray<int>                 cellIndices;
            ///массив направлений рыбок
            public NativeArray<Heading>             cellAlignment;
            ///"разделение" ячеек, хз пока, что под этим подразумевается
            public NativeArray<Position>            cellSeparation;
            ///индексы клеток, на которых есть акула
            public NativeArray<int>                 cellObstaclePositionIndex;
            ///массив дистанций до акул
            public NativeArray<float>               cellObstacleDistance;
            ///ID ячеек, на которых находятся Ведущие
            public NativeArray<int>                 cellTargetPistionIndex;
            ///количество ячеек - почему-то в коллекции.
            public NativeArray<int>                 cellCount; 
            [ReadOnly] public NativeArray<Position> targetPositions; //массив позиций Ведущих
            [ReadOnly] public NativeArray<Position> obstaclePositions; //массив позиций Акул

            /// <summary>
            /// void - походу для оптимальной работы BurstCompile
            /// TODO - переделать с void NearestPosition (* ,out int, out float ) на (int, float) NearestPosition
            /// заценим функционал кортежей свежего C# как бонус
            /// </summary>
            /// <param name="targets">массив позиций Ведущих</param>
            /// <param name="position">позиция рыбешки, вызвавшая метод</param>
            /// <param name="nearestPositionIndex">первое возвращаемое значение - ID ближайшей позиции</param>
            /// <param name="nearestDistance">второе возвращаемое значение -</param>
            void NearestPosition(NativeArray<Position> targets, float3 position, out int nearestPositionIndex, out float nearestDistance )
            {
                nearestPositionIndex = 0;
                nearestDistance      = math.lengthsq(position-targets[0].Value);
                //записали сюда квадрат длины расстояния между позицией рыбешки и позицией 0го ведущего
                //пока что 0й ведущий считается ближайшим

                //цикл для каждого таргета (кроме id 0) считает расстояние до рыбешки
                //сравнивает его с текущим минимальным, если оказывается меньше - замещает
                //что примечательно - для "если" не используется IF - вероятно, чтобы BurstCompile скушал
                for (int i = 1; i < targets.Length; i++)
                {
                    //var targetPosition = targets[i].Value; //по мнению разрабов это повышает читабельность, наверное
                    //var distance       = math.lengthsq(position-targetPosition);

                    //переделал под себя
                    //считаем квадрат длины
                    var distance = math.lengthsq(position - targets[i].Value);

                    ///классический код выглядел бы так:
                    //if (distance < nearestDistance
                    //{
                    //nearestDistance = distance;
                    //nearestPostitionIndex = i;
                    //}
                    //но у нас-то BustCompile-дружелюбный...
                    var nearest        = distance < nearestDistance;

                    nearestDistance      = math.select(nearestDistance, distance, nearest);
                    nearestPositionIndex = math.select(nearestPositionIndex, i, nearest);
                }
                nearestDistance = math.sqrt(nearestDistance);//т.к. мы брали квадрат дистанции - теперь берем корень.
                //мне не шибко нравится реализация:
                //1) изначально мы "испачкали" выходные значения, и потом их правили. Лучше бы задействовали их только в конце функции
                //2) а смысл вообще работать с реальной длиной, а не с квадратом длины? Взятие корня так-то не экономная операция
            }

            public void ExecuteFirst(int index)
            {
                var position = cellSeparation[index].Value / cellCount[index];

                int obstaclePositionIndex;
                float obstacleDistance;
                NearestPosition(obstaclePositions, position, out obstaclePositionIndex, out obstacleDistance);
                cellObstaclePositionIndex[index] = obstaclePositionIndex;
                cellObstacleDistance[index]      = obstacleDistance;

                int targetPositionIndex;
                float targetDistance;
                NearestPosition(targetPositions, position, out targetPositionIndex, out targetDistance);
                cellTargetPistionIndex[index] = targetPositionIndex;

                cellIndices[index] = index;
            }

            public void ExecuteNext(int cellIndex, int index)
            {
                cellCount[cellIndex]      += 1;
                cellAlignment[cellIndex]  = new Heading { Value = cellAlignment[cellIndex].Value + cellAlignment[index].Value };
                cellSeparation[cellIndex] = new Position { Value = cellSeparation[cellIndex].Value + cellSeparation[index].Value };
                cellIndices[index]        = cellIndex;
            }
        }

        [BurstCompile]
        [RequireComponentTag(typeof(Boid))]
        struct Steer : IJobProcessComponentDataWithEntity<Position, Heading>
        {
            [ReadOnly] public NativeArray<int>             cellIndices;
            [ReadOnly] public Boid                         settings;
            [ReadOnly] public NativeArray<Position>        targetPositions;
            [ReadOnly] public NativeArray<Position>        obstaclePositions;
            [ReadOnly] public NativeArray<Heading>         cellAlignment;
            [ReadOnly] public NativeArray<Position>        cellSeparation;
            [ReadOnly] public NativeArray<int>             cellObstaclePositionIndex;
            [ReadOnly] public NativeArray<float>           cellObstacleDistance;
            [ReadOnly] public NativeArray<int>             cellTargetPistionIndex;
            [ReadOnly] public NativeArray<int>             cellCount;
            public float                                   dt;

            public void Execute(Entity entity, int index, [ReadOnly]ref Position position, ref Heading heading)
            {
                var forward                           = heading.Value; 
                var currentPosition                   = position.Value;
                var cellIndex                         = cellIndices[index];
                var neighborCount                     = cellCount[cellIndex];
                var alignment                         = cellAlignment[cellIndex].Value;
                var separation                        = cellSeparation[cellIndex].Value;
                var nearestObstacleDistance           = cellObstacleDistance[cellIndex];
                var nearestObstaclePositionIndex      = cellObstaclePositionIndex[cellIndex];
                var nearestTargetPositionIndex        = cellTargetPistionIndex[cellIndex];
                var nearestObstaclePosition           = obstaclePositions[nearestObstaclePositionIndex].Value;
                var nearestTargetPosition             = targetPositions[nearestTargetPositionIndex].Value;

                var obstacleSteering                  = currentPosition - nearestObstaclePosition;
                var avoidObstacleHeading              = (nearestObstaclePosition + math.normalizesafe(obstacleSteering)
                                                        * settings.obstacleAversionDistance)- currentPosition;
                var targetHeading                     = settings.targetWeight
                                                        * math.normalizesafe(nearestTargetPosition - currentPosition);
                var nearestObstacleDistanceFromRadius = nearestObstacleDistance - settings.obstacleAversionDistance;
                var alignmentResult                   = settings.alignmentWeight
                                                        * math.normalizesafe((alignment/neighborCount)-forward);
                var separationResult                  = settings.separationWeight
                                                        * math.normalizesafe((currentPosition * neighborCount) - separation);
                var normalHeading                     = math.normalizesafe(alignmentResult + separationResult + targetHeading);
                var targetForward                     = math.select(normalHeading, avoidObstacleHeading, nearestObstacleDistanceFromRadius < 0);
                var nextHeading                       = math.normalizesafe(forward + dt*(targetForward-forward));

                heading = new Heading {Value = nextHeading};
            }
        }

        protected override void OnStopRunning()
        {
            for (var i = 0; i < m_PrevCells.Count; i++)
            {
                m_PrevCells[i].hashMap.Dispose();
                m_PrevCells[i].cellIndices.Dispose();
                m_PrevCells[i].copyTargetPositions.Dispose();
                m_PrevCells[i].copyObstaclePositions.Dispose();
                m_PrevCells[i].cellAlignment.Dispose();
                m_PrevCells[i].cellSeparation.Dispose();
                m_PrevCells[i].cellObstacleDistance.Dispose();
                m_PrevCells[i].cellObstaclePositionIndex.Dispose();
                m_PrevCells[i].cellTargetPistionIndex.Dispose();
                m_PrevCells[i].cellCount.Dispose();
            }
            m_PrevCells.Clear();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityManager.GetAllUniqueSharedComponentData(m_UniqueTypes);//типа подгружаем все возможные типы рыбок

            var obstacleCount = m_ObstacleGroup.CalculateLength(); //считаем акул. У нас всегда obstacleCount == 1.
            var targetCount = m_TargetGroup.CalculateLength(); //считаем ведущих. У нас всегда targetCount == 2/

            // Ingore typeIndex 0, can't use the default for anything meaningful.
            //поменял "typeIndex" на i - так привычнее
            for (int i = 1; i < m_UniqueTypes.Count; i++)
            {
                var settings = m_UniqueTypes[i];
                m_BoidGroup.SetFilter(settings);//найдем все рыбешки, которые базируются на типе m_UniqyeTypes[i]
                //рыбешки типа m_UniqyeTypes[0] не рассматриваются

                var boidCount  = m_BoidGroup.CalculateLength();//посчитаем их

                var cacheIndex                = i - 1; // индексу i=[1 : Count) соотвествует cacheIndex [0 - Count-1)

                ///создаем кучу массивов
                /// boidCount - столько элементов int будет помещаться в массив - меняется в течение цикла
                /// массив располагается в контейнере Allocator.TempJob (быстрый, но живет до 3х кадров)
                /// NativeArrayOptions.UninitializedMemory - режим, когда мы не проверяем, что именно находится в каждой ячейке

                //этот массив хранит ID ячеек рыбешек
                var cellIndices               = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

                //тут не массив, но хешмап. Занятно
                var hashMap                   = new NativeMultiHashMap<int,int>(boidCount,Allocator.TempJob);

                //этот массив хранит расстояния каждой рыбешки выбранного типа до акулы
                //(TODO - разобраться, скорее всего, до ближайшей акулы, но из названия это не очевидно) 
                var cellObstacleDistance      = new NativeArray<float>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

                //у каждой рыбки может быть своя ближайшая акула - поэтому есть этот массив.
                var cellObstaclePositionIndex = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);

                //у каждой рыбки при этом своя ведущая - в массив записан ID её ячейки
                var cellTargetPositionIndex   = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                //у каждой рыбки свой cellCount - TODO - разобраться
                var cellCount                 = new NativeArray<int>(boidCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
                
                ///а теперь разбираем ComponentGroup на составные компоненты, которые пихаем в  массивы

                //массив направлений рыбешек
                var cellAlignment             = m_BoidGroup.ToComponentDataArray<Heading>(Allocator.TempJob, out var initialCellAlignmentJobHandle);
                //массивы позиций рыбешек, ведущих и акул соответственно
                var cellSeparation            = m_BoidGroup.ToComponentDataArray<Position>(Allocator.TempJob, out var initialCellSeparationJobHandle);
                var copyTargetPositions       = m_TargetGroup.ToComponentDataArray<Position>(Allocator.TempJob, out var copyTargetPositionsJobHandle);
                var copyObstaclePositions     = m_ObstacleGroup.ToComponentDataArray<Position>(Allocator.TempJob, out var copyObstaclePositionsJobHandle);

                ///это структура => когда мы создаем её экземпляр, выделяем память и копируем все, что при инициализации использовалось
                ///т.е.
                var nextCells = new PrevCells
                {
                    //часть массивов не была проинициализирована, так что их копирование нужно только для выделения памяти
                    //(или мы именно ссылки на массивы копируем, под ссылки память уже выделена? TODO - разобраться)
                    cellIndices = cellIndices, //не инициализирован
                    hashMap                   = hashMap,//не инициализирован           
                    copyObstaclePositions     = copyObstaclePositions,//Ok
                    copyTargetPositions       = copyTargetPositions,//Ok
                    cellAlignment             = cellAlignment,//Ok
                    cellSeparation            = cellSeparation,//Ok
                    cellObstacleDistance      = cellObstacleDistance,//не инициализирован
                    cellObstaclePositionIndex = cellObstaclePositionIndex,//не инициализирован
                    cellTargetPistionIndex    = cellTargetPositionIndex,//не инициализирован
                    cellCount                 = cellCount//не инициализирован
                };

                //Имеем сравнение i-1 с 0-1, i при этом на первом входе =1 и инкрементируется
                //Лучше бы i начинали с 0, на мой взгляд - это во-первых
                //а во-вторых - m_PrevCells.Add вызывается только здесь, cacheIndex только инкрементирует
                //=> cacheIndex всегда больше m_PrevCells.Count-1,
                //else никогда не выполнится (TODO - проверить)
                if (cacheIndex > (m_PrevCells.Count - 1))
                {
                    m_PrevCells.Add(nextCells);
                }
                else
                {
                    Debug.Log("Сюда, теоретически, я не должен был попасть");
                    m_PrevCells[cacheIndex].hashMap.Dispose();
                    m_PrevCells[cacheIndex].cellIndices.Dispose();
                    m_PrevCells[cacheIndex].cellObstaclePositionIndex.Dispose();
                    m_PrevCells[cacheIndex].cellTargetPistionIndex.Dispose();
                    m_PrevCells[cacheIndex].copyTargetPositions.Dispose();
                    m_PrevCells[cacheIndex].copyObstaclePositions.Dispose();
                    m_PrevCells[cacheIndex].cellAlignment.Dispose();
                    m_PrevCells[cacheIndex].cellSeparation.Dispose();
                    m_PrevCells[cacheIndex].cellObstacleDistance.Dispose();
                    m_PrevCells[cacheIndex].cellCount.Dispose();
                }
                m_PrevCells[cacheIndex] = nextCells;//лишняя строчка - мы шагом ранее добавили уже ячейку

                var hashPositionsJob = new HashPositions
                {
                    hashMap        = hashMap.ToConcurrent(),
                    cellRadius     = settings.cellRadius
                };
                var hashPositionsJobHandle = hashPositionsJob.ScheduleGroup(m_BoidGroup, inputDeps);

                var initialCellCountJob = new MemsetNativeArray<int>
                {
                    Source = cellCount,
                    Value  = 1
                };
                var initialCellCountJobHandle = initialCellCountJob.Schedule(boidCount, 64, inputDeps);

                var initialCellBarrierJobHandle = JobHandle.CombineDependencies(initialCellAlignmentJobHandle, initialCellSeparationJobHandle, initialCellCountJobHandle);
                var copyTargetObstacleBarrierJobHandle = JobHandle.CombineDependencies(copyTargetPositionsJobHandle, copyObstaclePositionsJobHandle);
                var mergeCellsBarrierJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, initialCellBarrierJobHandle, copyTargetObstacleBarrierJobHandle);

                var mergeCellsJob = new MergeCells
                {
                    cellIndices               = cellIndices,
                    cellAlignment             = cellAlignment,
                    cellSeparation            = cellSeparation,
                    cellObstacleDistance      = cellObstacleDistance,
                    cellObstaclePositionIndex = cellObstaclePositionIndex,
                    cellTargetPistionIndex    = cellTargetPositionIndex,
                    cellCount                 = cellCount,
                    targetPositions           = copyTargetPositions,
                    obstaclePositions         = copyObstaclePositions
                };
                var mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap,64,mergeCellsBarrierJobHandle);

                var steerJob = new Steer
                {
                    cellIndices               = nextCells.cellIndices,
                    settings                  = settings,
                    cellAlignment             = cellAlignment,
                    cellSeparation            = cellSeparation,
                    cellObstacleDistance      = cellObstacleDistance,
                    cellObstaclePositionIndex = cellObstaclePositionIndex,
                    cellTargetPistionIndex    = cellTargetPositionIndex,
                    cellCount                 = cellCount,
                    targetPositions           = copyTargetPositions,
                    obstaclePositions         = copyObstaclePositions,
                    dt                        = Time.deltaTime,
                };
                var steerJobHandle = steerJob.ScheduleGroup(m_BoidGroup, mergeCellsJobHandle);

                inputDeps = steerJobHandle;
                m_BoidGroup.AddDependency(inputDeps);
            }
            m_UniqueTypes.Clear();

            return inputDeps;
        }

        /// <summary>
        /// начинаем мы отсюда
        /// </summary>
        protected override void OnCreateManager()
        {
            //кешируем сущности, содержащие одновременно Boid, Poistion, Heading
            //т.е. всх рыбешек
            m_BoidGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(Boid)),
                ComponentType.ReadOnly(typeof(Position)),
                typeof(Heading));
            ///тоже самое, но фильтруем Ведущих
            m_TargetGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(BoidTarget)),
                ComponentType.ReadOnly(typeof(Position)));
            /// и Акул
            m_ObstacleGroup = GetComponentGroup(
                ComponentType.ReadOnly(typeof(BoidObstacle)),
                ComponentType.ReadOnly(typeof(Position)));
        }
    }
}
