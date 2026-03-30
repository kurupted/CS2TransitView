using Game.Common;
using Game.Net;
using Game.Tools;
using System.Collections.Generic;
using BetterTransitView.Systems;
using Unity.Entities;

namespace BetterTransitView.Systems
{
    [UpdateAfter(typeof(TrafficUISystem))]
    public partial class TrafficHighlightSystem : SystemBase
    {
        private HashSet<Entity> highlightedEntities = new HashSet<Entity>();
        
        // Renamed to avoid conflict with the Class Name
        private TrafficUISystem m_TrafficUISystem; 

        protected override void OnCreate()
        {
            base.OnCreate();
            Enabled = true;
            m_TrafficUISystem = World.GetOrCreateSystemManaged<TrafficUISystem>();
        }

        protected override void OnUpdate()
        {
            if (TrafficUISystem.IsDirty)
            {
                UpdateHighlights();
                TrafficUISystem.IsDirty = false;
            }
        }

        private void UpdateHighlights()
        {
            HashSet<Entity> newSet = new HashSet<Entity>();

            // 1. ALWAYS Highlight Selected Lanes/Road
            // accessing the STATIC list on the CLASS
            /*if (TrafficUISystem.AnalyzedLanes != null) 
            {
                foreach (var entity in TrafficUISystem.AnalyzedLanes)
                {
                    if (EntityManager.Exists(entity)) newSet.Add(entity);
                }
            }*/

            // 2. CONDITIONALLY Highlight Agents
            // accessing the INSTANCE property on the SYSTEM
            if (m_TrafficUISystem.HighlightAgents && TrafficUISystem.CurrentRenderList != null)
            {
                foreach (var item in TrafficUISystem.CurrentRenderList)
                {
                    Entity agentToHighlight = item.sourceAgent != Entity.Null ? item.sourceAgent : item.entity;
                    if (EntityManager.Exists(agentToHighlight)) 
                    {
                        newSet.Add(agentToHighlight);
                        
                        // Highlight attached trailers, train carriages, etc.
                        if (EntityManager.HasBuffer<Game.Vehicles.LayoutElement>(agentToHighlight))
                        {
                            var layoutElements = EntityManager.GetBuffer<Game.Vehicles.LayoutElement>(agentToHighlight);
                            foreach (var layoutElement in layoutElements)
                            {
                                if (EntityManager.Exists(layoutElement.m_Vehicle))
                                {
                                    newSet.Add(layoutElement.m_Vehicle);
                                }
                            }
                        }
                    }
                    
                    if (item.destinationEntity != Entity.Null && EntityManager.Exists(item.destinationEntity))
                        newSet.Add(item.destinationEntity);
                }
            }

            // 3. Sync changes (Remove old, Add new)
            List<Entity> toRemove = new List<Entity>();
            foreach (var entity in highlightedEntities)
            {
                if (!newSet.Contains(entity)) toRemove.Add(entity);
            }

            foreach (var entity in toRemove)
            {
                if (EntityManager.Exists(entity))
                {
                    EntityManager.RemoveComponent<Highlighted>(entity);
                    EntityManager.AddComponent<BatchesUpdated>(entity);
                }
                highlightedEntities.Remove(entity);
            }

            foreach (var entity in newSet)
            {
                if (!highlightedEntities.Contains(entity))
                {
                    EntityManager.AddComponent<Highlighted>(entity);
                    EntityManager.AddComponent<BatchesUpdated>(entity);
                    highlightedEntities.Add(entity);
                }
            }
        }
    }
}