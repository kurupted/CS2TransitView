using Game.Creatures;
using Game.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace BetterTransitView.Jobs
{
    [BurstCompile]
    public struct CountLanesJob : IJob
    {
        [ReadOnly] public NativeArray<EntityRouteInput> input;
        [ReadOnly] public ComponentLookup<CarCurrentLane> carLaneLookup;
        [ReadOnly] public ComponentLookup<HumanCurrentLane> humanLaneLookup;
        
        public NativeHashMap<Entity, int> laneCounts;

        public void Execute()
        {
            for (int i = 0; i < input.Length; i++)
            {
                Entity entity = input[i].entity;
                Entity laneEntity = Entity.Null;

                if (carLaneLookup.TryGetComponent(entity, out CarCurrentLane carLane))
                {
                    laneEntity = carLane.m_Lane;
                }
                else if (humanLaneLookup.TryGetComponent(entity, out HumanCurrentLane humanLane))
                {
                    laneEntity = humanLane.m_Lane;
                }

                if (laneEntity != Entity.Null)
                {
                    if (laneCounts.ContainsKey(laneEntity))
                    {
                        laneCounts[laneEntity] += 1;
                    }
                    else
                    {
                        laneCounts.Add(laneEntity, 1);
                    }
                }
            }
        }
    }
}