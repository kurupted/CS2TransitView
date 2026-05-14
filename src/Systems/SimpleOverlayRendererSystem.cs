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
        private EntityQuery m_ResidentQuery;

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
            
            m_ResidentQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Game.Creatures.Resident>() },
                Any = new[] { 
                    ComponentType.ReadOnly<Game.Creatures.Queue>(),
                    ComponentType.ReadOnly<Game.Creatures.Creature>() 
                }
            });
            
        }

        protected override void OnUpdate()
        {
            if (_mTransitUISystem == null || !_mTransitUISystem.IsTransitPanelActive) return;

            var hiddenSet = new NativeHashSet<Entity>(TransitUISystem.HiddenCustomRoutes.Count, Allocator.TempJob);
            foreach (var e in TransitUISystem.HiddenCustomRoutes) hiddenSet.Add(e);

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle deps);
            
            // CONTAINERS
            var stopColors = new NativeParallelMultiHashMap<Entity, UnityEngine.Color>(30000, Allocator.TempJob);
            var stopPositions = new NativeHashMap<Entity, float3>(30000, Allocator.TempJob);
            var segmentToRouteMap = new NativeParallelMultiHashMap<Entity, Entity>(200000, Allocator.TempJob);
            var waypointColors = new NativeParallelMultiHashMap<Entity, UnityEngine.Color>(30000, Allocator.TempJob);
            var waypointPositions = new NativeHashMap<Entity, float3>(30000, Allocator.TempJob);

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
                PositionLookup = SystemAPI.GetComponentLookup<Game.Routes.Position>(true),
                DrawStops = TransitUISystem.ShowStopsAndStations,
                ConnectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                TransportStopLookup = SystemAPI.GetComponentLookup<Game.Routes.TransportStop>(true),
                ZoomLevel = m_CameraUpdateSystem.zoom,
                
                StopColors = stopColors,
                StopPositions = stopPositions,
                WaypointColors = waypointColors,
                WaypointPositions = waypointPositions,
                SharedSegmentsMap = segmentToRouteMap
            };
            
            // Schedule Render Job to wait for BOTH the Tally Job AND the Render Buffer
            JobHandle transitHandle = renderJob.Schedule(m_TransitLinesQuery, JobHandle.CombineDependencies(tallyHandle, deps));

            // Pass 2.5: Tally Passengers (Does not write to buffer, can run parallel with lines)
            NativeParallelMultiHashMap<Entity, int> passengerTallies = new NativeParallelMultiHashMap<Entity, int>(10000, Allocator.TempJob);
            JobHandle passengerTallyHandle = transitHandle; 
            if (TransitUISystem.ShowWaitingPassengers && TransitUISystem.ShowStopsAndStations)
            {
                var passengerTallyJob = new TallyWaitingPassengersJob
                {
                    ResidentType = SystemAPI.GetComponentTypeHandle<Game.Creatures.Resident>(true),
                    QueueBufferType = SystemAPI.GetBufferTypeHandle<Game.Creatures.Queue>(true),
                    CreatureType = SystemAPI.GetComponentTypeHandle<Game.Creatures.Creature>(true),
                    HumanLaneType = SystemAPI.GetComponentTypeHandle<Game.Creatures.HumanCurrentLane>(true),
                    ConnectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                    VisibleStops = stopPositions,
                    StopPassengerCounts = passengerTallies.AsParallelWriter()
                };
                passengerTallyHandle = passengerTallyJob.ScheduleParallel(m_ResidentQuery, transitHandle);
            }
            
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
                    HiddenRoutes = hiddenSet,
                    ZoomLevel = m_CameraUpdateSystem.zoom
                };
                vehicleHandle = drawVehiclesJob.Schedule(m_TransitLinesQuery, transitHandle);
            }

            // Grab Camera Data
            float3 camPos = float3.zero;
            float3 camRight = new float3(1, 0, 0);
            float3 camUp = new float3(0, 1, 0);
            if (UnityEngine.Camera.main != null) 
            {
                camPos = UnityEngine.Camera.main.transform.position;
                camRight = UnityEngine.Camera.main.transform.right;
                camUp = UnityEngine.Camera.main.transform.up;
            }

            // Combine dependencies so Stops wait for BOTH tallies to finish AND vehicles to finish drawing
            JobHandle preStopsHandle = JobHandle.CombineDependencies(passengerTallyHandle, vehicleHandle);

            // PASS 4: Draw Stops SECOND (Writes to buffer, layering ON TOP of vehicles)
            var drawStopsJob = new DrawTransitStopsJob
            {
                overlayBuffer = buffer,
                stopColors = stopColors,
                stopPositions = stopPositions,
                zoomLevel = m_CameraUpdateSystem.zoom,
                drawStops = TransitUISystem.ShowStopsAndStations,
                showWaiting = TransitUISystem.ShowWaitingPassengers,
                passengerTallies = passengerTallies,
                cameraRight = camRight,
                cameraUp = camUp,
                cameraPosition = camPos 
            };

            JobHandle drawStopsHandle = drawStopsJob.Schedule(preStopsHandle);

            // PASS 5: Draw Waypoints THIRD
            var drawWaypointsJob = new DrawTransitWaypointsJob
            {
                overlayBuffer = buffer,
                waypointColors = waypointColors,
                waypointPositions = waypointPositions,
                zoomLevel = m_CameraUpdateSystem.zoom
            };
            
            JobHandle waypointsHandle = drawWaypointsJob.Schedule(drawStopsHandle);

            // The final dependency
            JobHandle finalDeps = waypointsHandle;
            
            // CLEANUP: Dispose of everything safely using the job handles that finished using them
            segmentToRouteMap.Dispose(transitHandle); 
            hiddenSet.Dispose(finalDeps);
            stopColors.Dispose(drawStopsHandle);
            stopPositions.Dispose(drawStopsHandle);
            waypointColors.Dispose(waypointsHandle);
            waypointPositions.Dispose(waypointsHandle);
            
            // Wait for finalDeps before disposing the tallies, just to be safe
            passengerTallies.Dispose(finalDeps);

            Dependency = finalDeps;
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