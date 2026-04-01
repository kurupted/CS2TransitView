using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Rendering;
using Game.Routes;
using Game.Tools;
using Game.Pathfind; // Required for PathElement
using Game.Prefabs; // Required for TransportLineData
using BetterTransitView.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Color = UnityEngine.Color;

namespace BetterTransitView.Systems
{
    public partial class SimpleOverlayRendererSystem : SystemBase
    {
        private OverlayRenderSystem m_OverlayRenderSystem;
        private TransitUISystem _mTransitUISystem;
        private CameraUpdateSystem m_CameraUpdateSystem; 
        private EntityQuery m_TransitLinesQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_OverlayRenderSystem = World.GetExistingSystemManaged<OverlayRenderSystem>();
            _mTransitUISystem = World.GetOrCreateSystemManaged<TransitUISystem>();
            m_CameraUpdateSystem = World.GetExistingSystemManaged<CameraUpdateSystem>();

            m_TransitLinesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { 
                    ComponentType.ReadOnly<Route>(), 
                    ComponentType.ReadOnly<Game.Routes.Color>(), 
                    ComponentType.ReadOnly<RouteSegment>() 
                },
                None = new[] { 
                    ComponentType.ReadOnly<Deleted>(), 
                    ComponentType.ReadOnly<Game.Tools.Temp>() // Explicit namespace fix
                }
            });
        }

        protected override void OnUpdate()
        {
            if (_mTransitUISystem == null || !_mTransitUISystem.IsTransitPanelActive) return;

            var hiddenSet = new NativeHashSet<Entity>(TransitUISystem.HiddenCustomRoutes.Count, Allocator.TempJob);
            foreach (var e in TransitUISystem.HiddenCustomRoutes) hiddenSet.Add(e);

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle deps);
            
            // CONTAINERS - Drastically increased capacity to prevent Parallel Writer corruption/freezes!
            var stopColors = new NativeParallelMultiHashMap<Entity, UnityEngine.Color>(30000, Allocator.TempJob);
            var stopPositions = new NativeHashMap<Entity, float3>(30000, Allocator.TempJob);
            var segmentToRouteMap = new NativeParallelMultiHashMap<Entity, Entity>(200000, Allocator.TempJob); // 200k ensures no freezing
            
            // PASS 1: Tally Shared Segments
            var tallyJob = new TallySharedSegmentsJob
            {
                EntityHandle = SystemAPI.GetEntityTypeHandle(),
                SegmentBufferType = SystemAPI.GetBufferTypeHandle<RouteSegment>(true),
                PathElementLookup = SystemAPI.GetBufferLookup<PathElement>(true),
                HiddenRouteType = SystemAPI.GetComponentTypeHandle<HiddenRoute>(true),
                SegmentToRouteMap = segmentToRouteMap.AsParallelWriter()
            };
            
            JobHandle tallyHandle = tallyJob.ScheduleParallel(m_TransitLinesQuery, Dependency);
            
            // PASS 2: Render Lines (Calculates Ribbon Offsets)
            var renderJob = new RenderTransitLineOverlayJob
            {
                overlayBuffer = buffer,
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ColorType = SystemAPI.GetComponentTypeHandle<Game.Routes.Color>(true),
                SegmentBufferType = SystemAPI.GetBufferTypeHandle<RouteSegment>(true),
                PathElementLookup = SystemAPI.GetBufferLookup<PathElement>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                TransportLineDataLookup = SystemAPI.GetComponentLookup<TransportLineData>(true),
                HiddenRoutes = hiddenSet,
                WaypointBufferType = SystemAPI.GetBufferTypeHandle<Game.Routes.RouteWaypoint>(true),
                TransformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                DrawStops = TransitUISystem.ShowStopsAndStations,
                ConnectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                ZoomLevel = m_CameraUpdateSystem.zoom,
                
                StopColors = stopColors,
                StopPositions = stopPositions,
                SharedSegmentsMap = segmentToRouteMap
            };
            
            // Schedule Render Job to wait for BOTH the Tally Job AND the Render Buffer
            JobHandle transitHandle = renderJob.Schedule(m_TransitLinesQuery, JobHandle.CombineDependencies(tallyHandle, deps));

            // PASS 3: Draw Stops (Pie Charts)
            var drawStopsJob = new DrawTransitStopsJob
            {
                overlayBuffer = buffer,
                stopColors = stopColors,
                stopPositions = stopPositions,
                zoomLevel = m_CameraUpdateSystem.zoom,
                drawStops = TransitUISystem.ShowStopsAndStations
            };

            JobHandle drawStopsHandle = drawStopsJob.Schedule(transitHandle);

            // CLEANUP: Dispose of everything safely using the job handles that finished using them
            segmentToRouteMap.Dispose(transitHandle); 
            hiddenSet.Dispose(drawStopsHandle);
            stopColors.Dispose(drawStopsHandle);
            stopPositions.Dispose(drawStopsHandle);

            Dependency = drawStopsHandle;
            m_OverlayRenderSystem.AddBufferWriter(Dependency);
        }
        

        // Wrapper methods for TrafficRouteSystem compatibility
        public Buffer GetBuffer(out JobHandle dependencies)
        {
            return new Buffer(m_OverlayRenderSystem.GetBuffer(out dependencies));
        }

        public void AddBufferWriter(JobHandle handle)
        {
            m_OverlayRenderSystem.AddBufferWriter(handle);
        }

        public struct Buffer
        {
            private OverlayRenderSystem.Buffer m_Buffer;
            public Buffer(OverlayRenderSystem.Buffer buffer) { m_Buffer = buffer; }
            public void DrawCurve(Color color, Bezier4x3 curve, float width, float2 roundness)
            { m_Buffer.DrawCurve(color, curve, width, roundness); }
            public void DrawLine(Color color, Line3.Segment line, float width)
            { m_Buffer.DrawLine(color, line, width); }
        }
    }
}