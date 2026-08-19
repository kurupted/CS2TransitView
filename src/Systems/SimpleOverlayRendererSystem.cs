using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Rendering;
using Game.Routes;
using Game.Tools;
using Game.Pathfind; // Required for PathElement
using Game.Prefabs; // Required for TransportLineData
using BetterTransitView.Jobs;
using BetterTransitView.Utils;
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

        // Persistent Native Containers (Zero allocation per frame)
        private NativeHashSet<Entity> m_HiddenSet;
        private NativeParallelMultiHashMap<Entity, UnityEngine.Color> m_StopColors;
        private NativeHashMap<Entity, float3> m_StopPositions;
        private NativeParallelMultiHashMap<Entity, Entity> m_SegmentToRouteMap;
        private NativeParallelMultiHashMap<Entity, UnityEngine.Color> m_WaypointColors;
        private NativeHashMap<Entity, float3> m_WaypointPositions;
        private UnityEngine.Camera m_CachedMainCamera;

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

            // Initialize persistent containers once
            m_HiddenSet = new NativeHashSet<Entity>(128, Allocator.Persistent);
            m_StopColors = new NativeParallelMultiHashMap<Entity, UnityEngine.Color>(30000, Allocator.Persistent);
            m_StopPositions = new NativeHashMap<Entity, float3>(30000, Allocator.Persistent);
            m_SegmentToRouteMap = new NativeParallelMultiHashMap<Entity, Entity>(200000, Allocator.Persistent);
            m_WaypointColors = new NativeParallelMultiHashMap<Entity, UnityEngine.Color>(30000, Allocator.Persistent);
            m_WaypointPositions = new NativeHashMap<Entity, float3>(30000, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (m_HiddenSet.IsCreated) m_HiddenSet.Dispose();
            if (m_StopColors.IsCreated) m_StopColors.Dispose();
            if (m_StopPositions.IsCreated) m_StopPositions.Dispose();
            if (m_SegmentToRouteMap.IsCreated) m_SegmentToRouteMap.Dispose();
            if (m_WaypointColors.IsCreated) m_WaypointColors.Dispose();
            if (m_WaypointPositions.IsCreated) m_WaypointPositions.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_mTransitUISystem == null || !_mTransitUISystem.IsTransitPanelActive) return;

            // Clear containers for fresh frame
            m_HiddenSet.Clear();
            foreach (var e in TransitUISystem.HiddenCustomRoutes) m_HiddenSet.Add(e);

            m_StopColors.Clear();
            m_StopPositions.Clear();
            m_SegmentToRouteMap.Clear();
            m_WaypointColors.Clear();
            m_WaypointPositions.Clear();

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle deps);

            // PASS 1: Tally Shared Segments
            var tallyJob = new TallySharedSegmentsJob
            {
                EntityHandle = SystemAPI.GetEntityTypeHandle(),
                SegmentBufferType = SystemAPI.GetBufferTypeHandle<RouteSegment>(true),
                PathElementLookup = SystemAPI.GetBufferLookup<PathElement>(true),
                HiddenRouteType = SystemAPI.GetComponentTypeHandle<HiddenRoute>(true),
                SegmentToRouteMap = m_SegmentToRouteMap.AsParallelWriter()
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
                HiddenRoutes = m_HiddenSet,
                WaypointBufferType = SystemAPI.GetBufferTypeHandle<Game.Routes.RouteWaypoint>(true),
                TransformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                PositionLookup = SystemAPI.GetComponentLookup<Game.Routes.Position>(true),
                DrawStops = TransitUISystem.ShowStopsAndStations,
                ConnectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                TransportStopLookup = SystemAPI.GetComponentLookup<Game.Routes.TransportStop>(true),
                ZoomLevel = m_CameraUpdateSystem.zoom,
                
                StopColors = m_StopColors,
                StopPositions = m_StopPositions,
                WaypointColors = m_WaypointColors,
                WaypointPositions = m_WaypointPositions,
                SharedSegmentsMap = m_SegmentToRouteMap
            };
            
            // Schedule Render Job to wait for BOTH the Tally Job AND the Render Buffer
            JobHandle transitHandle = renderJob.Schedule(m_TransitLinesQuery, JobHandle.CombineDependencies(tallyHandle, deps));

            // Pass 3: Draw Vehicles FIRST (Writes to buffer)
            JobHandle vehicleHandle = transitHandle;
            if (TransitUISystem.ShowTransitVehicles)
            {
                var drawVehiclesJob = new DrawTransitVehiclesJob
                {
                    overlayBuffer = buffer,
                    EntityType = SystemAPI.GetEntityTypeHandle(),
                    RouteVehicleBufferType = SystemAPI.GetBufferTypeHandle<RouteVehicle>(true),
                    ColorType = SystemAPI.GetComponentTypeHandle<Game.Routes.Color>(true),
                    InterpolatedTransformLookup = SystemAPI.GetComponentLookup<Game.Rendering.InterpolatedTransform>(true),
                    TransformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                    HiddenRoutes = m_HiddenSet,
                    ZoomLevel = m_CameraUpdateSystem.zoom
                };
                vehicleHandle = drawVehiclesJob.Schedule(m_TransitLinesQuery, transitHandle);
            }

            // Grab Camera Data safely without scene-wide GameObject tag search
            float3 camPos = float3.zero;
            float3 camRight = new float3(1, 0, 0);
            float3 camUp = new float3(0, 1, 0);
            var camera = m_CameraUpdateSystem != null ? m_CameraUpdateSystem.activeCamera : null;
            if (camera == null)
            {
                if (m_CachedMainCamera == null) m_CachedMainCamera = UnityEngine.Camera.main;
                camera = m_CachedMainCamera;
            }
            if (camera != null) 
            {
                var camTrans = camera.transform;
                camPos = camTrans.position;
                camRight = camTrans.right;
                camUp = camTrans.up;
            }

            // PASS 4: Draw Stops SECOND (Writes to buffer, layering ON TOP of vehicles)
            var drawStopsJob = new DrawTransitStopsJob
            {
                overlayBuffer = buffer,
                stopColors = m_StopColors,
                stopPositions = m_StopPositions,
                zoomLevel = m_CameraUpdateSystem.zoom,
                drawStops = TransitUISystem.ShowStopsAndStations,
                showWaiting = TransitUISystem.ShowWaitingPassengers,
                showAverageWaitTime = BetterTransitView.ModSettings.ModSettings.Instance.ShowAverageWaitTime,
                cameraRight = camRight,
                cameraUp = camUp,
                cameraPosition = camPos,
                waitTimeDisplayFactor = Time2WorkInterop.GetTimeFactor(),
                
                // Injecting the ECS lookups directly so the Job can process the ConnectedRoute buffer
                ConnectedRouteLookup = SystemAPI.GetBufferLookup<Game.Routes.ConnectedRoute>(true),
                WaitingPassengersLookup = SystemAPI.GetComponentLookup<Game.Routes.WaitingPassengers>(true),
                OwnerLookup = SystemAPI.GetComponentLookup<Game.Common.Owner>(true),
                ColorLookup = SystemAPI.GetComponentLookup<Game.Routes.Color>(true),
                HiddenRoutes = m_HiddenSet
            };

            JobHandle drawStopsHandle = drawStopsJob.Schedule(vehicleHandle);

            // PASS 5: Draw Waypoints THIRD
            var drawWaypointsJob = new DrawTransitWaypointsJob
            {
                overlayBuffer = buffer,
                waypointColors = m_WaypointColors,
                waypointPositions = m_WaypointPositions,
                zoomLevel = m_CameraUpdateSystem.zoom
            };
            
            JobHandle waypointsHandle = drawWaypointsJob.Schedule(drawStopsHandle);

            // The final dependency
            Dependency = waypointsHandle;
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