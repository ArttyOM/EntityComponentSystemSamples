using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Samples.Common
{
    /// <summary>
    /// тэкс, здесь мы без многопотока, т.к. нам всего 2 компонента ковырять - незачем JobSystem разворачивать.
    /// </summary>
    public class SpawnRandomInSphereSystem : ComponentSystem
    {
        struct SpawnRandomInSphereInstance
        {
            public int spawnerIndex;
            public Entity sourceEntity;
            public float3 position;
        }

        ComponentGroup m_MainGroup;

        protected override void OnCreateManager()
        {
            m_MainGroup = GetComponentGroup(typeof(SpawnRandomInSphere), typeof(Position));
        }

        protected override void OnUpdate()
        {
            var uniqueTypes = new List<SpawnRandomInSphere>(10);//List - пустой пока что, просто емкость задана

            EntityManager.GetAllUniqueSharedComponentData(uniqueTypes);
            //походу эта эбола берет пустой список, смотрит на тип, под который он заточен, находит сущности, которые можно туда воткнуть, втыкает

            //не удержался от рефакторинга. Хотят работать с for - пусть фигачат с индексами. А так лучше уж через foreach
            //TODO: разобраться, пока вижу макаронный код только
            int spawnInstanceCount = 0;
            if (uniqueTypes.Count>0)
                foreach (var spawner in uniqueTypes)
            {
                m_MainGroup.SetFilter(spawner);//фильтруем сами себя. Т.е. 1 объект из всех выбираем
                var entityCount = m_MainGroup.CalculateLength(); //после этого считаем, какой же длины вышла коллекция. Ответ, конечно же, 1
                spawnInstanceCount += entityCount; //а после этого добавляем этот 1 к числу spawnInstanceCount. На кой черт?
            }//в результате этих костылей на выходе получаем "2". Круто, да?


            /*
            int spawnInstanceCount = 0;
            for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
            {
                var spawner = uniqueTypes[sharedIndex];
                m_MainGroup.SetFilter(spawner);
                var entityCount = m_MainGroup.CalculateLength();
                spawnInstanceCount += entityCount;
            }
            */

            Debug.Log("Я вангую, тут 2: "+ spawnInstanceCount);
            if (spawnInstanceCount == 0)
                return;

            var spawnInstances = new NativeArray<SpawnRandomInSphereInstance>(spawnInstanceCount, Allocator.Temp);
            {
                int spawnIndex = 0;
                for (int sharedIndex = 0; sharedIndex != uniqueTypes.Count; sharedIndex++)
                {
                    var spawner = uniqueTypes[sharedIndex];
                    m_MainGroup.SetFilter(spawner);

                    if (m_MainGroup.CalculateLength() == 0)//чуть ли не полное дублирование блока выше...
                        continue;
 
                    var entities = m_MainGroup.ToEntityArray(Allocator.TempJob); //т.е. мы берем одну сущность, передаем её в TempJob, получаем массив длиной в 2.
                    var positions = m_MainGroup.ToComponentDataArray<Position>(Allocator.TempJob); //во, наконец позицию забрали.
                    //после этих двух строчек и после выполнения цикла, теоретически, Allocator.TempJob содержит в качестве списка зависимостей массив из 2х сущностей и массив из 2х позиций

                    for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
                    {
                        var spawnInstance = new SpawnRandomInSphereInstance();
                        ///Напоминалка: SpawnRandomInSphereInstance -
                        ///           public int spawnerIndex;
                        ///           public Entity sourceEntity;
                        ///           public float3 position;



                        spawnInstance.sourceEntity = entities[entityIndex]; //ок, кешируем, какая сущность создала этот объект
                        spawnInstance.spawnerIndex = sharedIndex; //ок, кешируем ID спавнера
                        spawnInstance.position = positions[entityIndex].Value; //ок, кешируем позицию
                        //т.е. сначала мы аккуратненько собирали массив entities и массив positions так, чтобы взяв id сущности, такой же id подойдет к позиции
                        //а потом забиваем болт и фигачим в массив структур =D, по-моему намного проще можно было сделать TODO - попробовать

                        

                        spawnInstances[spawnIndex] = spawnInstance;
                        spawnIndex++;
                    }

                    entities.Dispose();
                    positions.Dispose();
                }
            }//... и вот, на выходе мы получили NativeArray<SpawnRandomInSphereInstance> из 2х экземплеяров структур
            ///как по мне, намного дешевле было без геммороя структуру отметить как [System.Serialisable], и добавить в качестве поля в Monobehaviour (или ComponentData+ ComponentDataWrapper)
            ///TODO - попробовать

            for (int spawnIndex = 0; spawnIndex < spawnInstances.Length; spawnIndex++)
            {
                int spawnerIndex = spawnInstances[spawnIndex].spawnerIndex;
                var spawner = uniqueTypes[spawnerIndex];
                int count = spawner.count;
                var entities = new NativeArray<Entity>(count,Allocator.Temp);
                var prefab = spawner.prefab;
                float radius = spawner.radius;
                var spawnPositions = new NativeArray<float3>(count, Allocator.Temp);
                float3 center = spawnInstances[spawnIndex].position;
                var sourceEntity = spawnInstances[spawnIndex].sourceEntity;

                GeneratePoints.RandomPointsInSphere(center,radius,ref spawnPositions);

                EntityManager.Instantiate(prefab, entities); ///вот он, факт создания новой сущности. Ура-ура.

                for (int i = 0; i < count; i++)
                {
                    var position = new Position
                    {
                        Value = spawnPositions[i]
                    };
                    EntityManager.SetComponentData(entities[i],position);
                }

                EntityManager.RemoveComponent<SpawnRandomInSphere>(sourceEntity); //когда спавнер отработал свое - просто с него снимаем компонент, он и отвалится

                spawnPositions.Dispose();
                entities.Dispose();
            }
            spawnInstances.Dispose();
        }
    }
}
