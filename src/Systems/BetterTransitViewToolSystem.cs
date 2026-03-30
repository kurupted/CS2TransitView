using Colossal.Entities;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Game.Routes;
using UnityEngine;
using Unity.Collections;
using PedestrianLane = Game.Net.PedestrianLane;
using TrackLane = Game.Net.TrackLane;

namespace BetterTransitView.Systems
{
    public partial class BetterTransitViewToolSystem : ToolBaseSystem
    {
        public override string toolID => "BetterTransitViewTool";

        private ToolSystem _toolSystem;
        private TrafficUISystem _uiSystem;
        private Entity _hoveredEntity = Entity.Null;
        private EntityQuery m_HighlightedQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _uiSystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
            m_HighlightedQuery = GetEntityQuery(ComponentType.ReadWrite<Highlighted>());
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            
            m_ToolRaycastSystem.typeMask = TypeMask.Net | TypeMask.StaticObjects;
            
            m_ToolRaycastSystem.netLayerMask = Layer.Road | Layer.PublicTransportRoad | Layer.Pathway | 
                                               Layer.TrainTrack | Layer.TramTrack | Layer.SubwayTrack;
                                               
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_toolSystem.activeTool != this) return inputDeps;

            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;

            Entity hitEntity = Entity.Null;
            if ((m_ToolRaycastSystem.raycastFlags & RaycastFlags.UIDisable) == 0)
            {
                if (GetRaycastResult(out Entity entity, out _))
                {
                    Entity potentialTarget = entity;

                    // 1. OWNER CHECK (Handle clicking props/sub-objects)
                    // If we click a bench or a tree attached to a road/station, 
                    // check its owner to see if the OWNER is a valid target.
                    if (!IsValidSpyTarget(potentialTarget) && EntityManager.HasComponent<Owner>(potentialTarget))
                    {
                        // Only inherit the owner if we clicked a physical object. 
                        // Game.Objects.Static covers all physical props, trees, and shelters!
                        // This prevents hovering over abstract floating text/labels from selecting the road.
                        if (EntityManager.HasComponent<Game.Objects.Static>(potentialTarget))
                        {
                            potentialTarget = EntityManager.GetComponentData<Owner>(potentialTarget).m_Owner;
                        }
                    }

                    // 2. FINAL VALIDATION
                    if (IsValidSpyTarget(potentialTarget))
                    {
                        hitEntity = potentialTarget;
                    }
                }
            }

            UpdateHoverHighlight(hitEntity);

            if (hitEntity != Entity.Null && applyAction.WasReleasedThisFrame())
            {
                _toolSystem.selected = hitEntity;
                Disable(); 
            }

            if (cancelAction.WasPressedThisFrame())
            {
                _uiSystem.SetSpyMode(false);
                Disable();
            }

            return inputDeps;
        }

        private bool IsValidSpyTarget(Entity entity)
        {
            if (entity == Entity.Null) return false;

            // A. Networks (Roads, Rails, Paths, Waterways)
            // Checking Edge/Node ensures standalone pathways are clickable because ALL networks use them.
            // This safely replaces the need to check for missing Pathway or PedestrianLane components.
            if (EntityManager.HasComponent<Game.Net.Edge>(entity) || 
                EntityManager.HasComponent<Game.Net.Node>(entity))
            {
                return true;
            }

            // B. Transit Stations 
            if (EntityManager.HasComponent<Game.Buildings.TransportStation>(entity))
            {
                return true;
            }
            
            // C. Direct Stops (If a stop icon is directly clickable, reject bicycles)
            if (EntityManager.HasComponent<Game.Routes.TransportStop>(entity))
            {
                if (EntityManager.HasComponent<Game.Routes.BusStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.TrainStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.TramStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.SubwayStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.ShipStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.AirplaneStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.FerryStop>(entity) ||
                    EntityManager.HasComponent<Game.Routes.TaxiStand>(entity))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateHoverHighlight(Entity newHover)
        {
            if (newHover == _hoveredEntity) return;

            // Remove old hover highlights
            if (_hoveredEntity != Entity.Null && _toolSystem.selected != _hoveredEntity)
            {
                EntityManager.RemoveComponent<Highlighted>(_hoveredEntity);
                EntityManager.AddComponent<BatchesUpdated>(_hoveredEntity);
        
                // ONLY highlight sublanes if the hovered entity is an intersection node
                if (EntityManager.HasComponent<Game.Net.Node>(_hoveredEntity) && 
                    EntityManager.TryGetBuffer(_hoveredEntity, true, out DynamicBuffer<Game.Net.SubLane> subLanes)) 
                {
                    foreach (var lane in subLanes) {
                        if (EntityManager.Exists(lane.m_SubLane)) {
                            EntityManager.RemoveComponent<Highlighted>(lane.m_SubLane);
                            EntityManager.AddComponent<BatchesUpdated>(lane.m_SubLane);
                        }
                    }
                }
            }

            // Apply new hover highlights
            if (newHover != Entity.Null)
            {
                EntityManager.AddComponent<Highlighted>(newHover);
                EntityManager.AddComponent<BatchesUpdated>(newHover);
        
                // ONLY highlight sublanes if the hovered entity is an intersection node
                if (EntityManager.HasComponent<Game.Net.Node>(newHover) && 
                    EntityManager.TryGetBuffer(newHover, true, out DynamicBuffer<Game.Net.SubLane> subLanes)) 
                {
                    foreach (var lane in subLanes) {
                        if (EntityManager.Exists(lane.m_SubLane)) {
                            EntityManager.AddComponent<Highlighted>(lane.m_SubLane);
                            EntityManager.AddComponent<BatchesUpdated>(lane.m_SubLane);
                        }
                    }
                }
            }

            _hoveredEntity = newHover;
        }
        
        public void Enable()
        {
            _toolSystem.activeTool = this;
            
            // Clear any lingering highlights left by the default tool before we start
            using var entities = m_HighlightedQuery.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                EntityManager.RemoveComponent<Highlighted>(e);
                EntityManager.AddComponent<BatchesUpdated>(e);
            }
        }

        public void Disable()
        {
            if (_toolSystem.activeTool == this)
            {
                _toolSystem.activeTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            }
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            // Cleanup highlights
            if (_hoveredEntity != Entity.Null)
            {
                EntityManager.RemoveComponent<Highlighted>(_hoveredEntity);
                EntityManager.AddComponent<BatchesUpdated>(_hoveredEntity);
         
                // ONLY remove sublane highlights if it was a node
                if (EntityManager.HasComponent<Game.Net.Node>(_hoveredEntity) && 
                    EntityManager.TryGetBuffer(_hoveredEntity, true, out DynamicBuffer<Game.Net.SubLane> subLanes)) 
                {
                    foreach (var lane in subLanes) {
                        if (EntityManager.Exists(lane.m_SubLane)) {
                            EntityManager.RemoveComponent<Highlighted>(lane.m_SubLane);
                            EntityManager.AddComponent<BatchesUpdated>(lane.m_SubLane);
                        }
                    }
                }
                _hoveredEntity = Entity.Null;
            }
        }

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;
    }
}