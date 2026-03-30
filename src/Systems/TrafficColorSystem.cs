using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BetterTransitView.Systems
{
    // CRITICAL FIX: Run in the Presentation System Group so the vanilla game doesn't overwrite us!
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(ObjectColorSystem))] 
    public partial class TrafficColorSystem : GameSystemBase
    {
        private TrafficUISystem m_TrafficUISystem;
        private EntityQuery m_TransitBuildingQuery;
        private EntityQuery m_ActiveInfomodeQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TrafficUISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();

            m_TransitBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadWrite<Game.Objects.Color>() },
                Any = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Buildings.TransportStation>()
                },
                None = new ComponentType[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });

            m_ActiveInfomodeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<InfoviewBuildingStatusData>(),
                    ComponentType.ReadOnly<InfomodeActive>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!m_TrafficUISystem.IsTransitPanelActive) return;

            byte stationIndex = 0;
            bool foundStation = false;

            var statusDatas = m_ActiveInfomodeQuery.ToComponentDataArray<InfoviewBuildingStatusData>(Allocator.Temp);
            var activeDatas = m_ActiveInfomodeQuery.ToComponentDataArray<InfomodeActive>(Allocator.Temp);

            for (int i = 0; i < statusDatas.Length; i++)
            {
                int currentType = (int)statusDatas[i].m_Type;
                if (currentType == 101) 
                {
                    stationIndex = (byte)activeDatas[i].m_Index;
                    foundStation = true;
                }
            }

            statusDatas.Dispose();
            activeDatas.Dispose();

            if (!foundStation) return;

            var colorJob = new ColorTransitBuildingsJob
            {
                ColorTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Objects.Color>(false),
                StationTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Buildings.TransportStation>(true),
                PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                BuildingDataLookup = SystemAPI.GetComponentLookup<BuildingData>(true),

                StationIndex = stationIndex,
                HasStationMode = foundStation
            };

            Dependency = colorJob.ScheduleParallel(m_TransitBuildingQuery, Dependency);
        }

        [BurstCompile]
        private struct ColorTransitBuildingsJob : IJobChunk
        {
            public ComponentTypeHandle<Game.Objects.Color> ColorTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Buildings.TransportStation> StationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> PrefabRefTypeHandle;
            [ReadOnly] public ComponentLookup<BuildingData> BuildingDataLookup;

            public byte StationIndex;
            public bool HasStationMode;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                NativeArray<Game.Objects.Color> colors = chunk.GetNativeArray(ref ColorTypeHandle);
                NativeArray<PrefabRef> prefabRefs = chunk.Has(ref PrefabRefTypeHandle) ? chunk.GetNativeArray(ref PrefabRefTypeHandle) : default;

                bool isStation = chunk.Has(ref StationTypeHandle);

                byte targetIndex;
                if (isStation && HasStationMode) targetIndex = StationIndex;
                else return; 

                for (int i = 0; i < colors.Length; i++)
                {
                    var colorComponent = colors[i];
                    
                    colorComponent.m_Index = targetIndex; 
                    colorComponent.m_Value = 255; 

                    // Ensure the pavement gets colored too!
                    if (prefabRefs.IsCreated && BuildingDataLookup.TryGetComponent(prefabRefs[i].m_Prefab, out BuildingData buildingData))
                    {
                        if ((buildingData.m_Flags & Game.Prefabs.BuildingFlags.ColorizeLot) != 0)
                        {
                            colorComponent.m_SubColor = true;
                        }
                    }
                    
                    colors[i] = colorComponent;
                }
            }
        }
    }
}