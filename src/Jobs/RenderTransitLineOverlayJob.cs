using System;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Routes;
using Game.Pathfind; 
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BetterTransitView.Jobs
{
    [BurstCompile]
    public struct RenderTransitLineOverlayJob : IJobChunk
    {
        public OverlayRenderSystem.Buffer overlayBuffer; 
        
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public ComponentTypeHandle<Game.Routes.Color> ColorType;
        [ReadOnly] public BufferTypeHandle<RouteSegment> SegmentBufferType;
        
        [ReadOnly] public BufferTypeHandle<RouteWaypoint> WaypointBufferType;
        [ReadOnly] public ComponentLookup<Game.Routes.Connected> ConnectedLookup; 
        [ReadOnly] public ComponentLookup<Game.Objects.Transform> TransformLookup;
        public bool DrawStops;
        
        [ReadOnly] public BufferLookup<PathElement> PathElementLookup;
        [ReadOnly] public ComponentLookup<Curve> CurveLookup;
        [ReadOnly] public NativeHashSet<Entity> HiddenRoutes;

        [ReadOnly] public ComponentLookup<PrefabRef> PrefabRefLookup;
        [ReadOnly] public ComponentLookup<TransportLineData> TransportLineDataLookup;
        public float ZoomLevel; 
        
        // Output Containers
        public NativeParallelMultiHashMap<Entity, UnityEngine.Color> StopColors;
        public NativeHashMap<Entity, float3> StopPositions;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);
            NativeArray<Game.Routes.Color> colors = chunk.GetNativeArray(ref ColorType);
            BufferAccessor<RouteSegment> segmentAccess = chunk.GetBufferAccessor(ref SegmentBufferType);
            
            bool hasWaypoints = chunk.Has(ref WaypointBufferType);
            BufferAccessor<RouteWaypoint> waypointAccess = hasWaypoints ? chunk.GetBufferAccessor(ref WaypointBufferType) : default;

            float minZoom = 1600f;
            float maxZoom = 10000f;
            float normalizedZoom = math.clamp((ZoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float baseWidth = 4.0f;
            float maxWidth = baseWidth * 12f; 
            float thickness = math.lerp(baseWidth, maxWidth, normalizedZoom);

            for (int i = 0; i < chunk.Count; i++)
            {
                Entity routeEntity = entities[i];
                if (HiddenRoutes.Contains(routeEntity)) continue;

                if (PrefabRefLookup.TryGetComponent(routeEntity, out var prefabRef) &&
                    TransportLineDataLookup.TryGetComponent(prefabRef.m_Prefab, out var lineData))
                {
                    var t = lineData.m_TransportType;
                    if (t != TransportType.Bus && t != TransportType.Train && t != TransportType.Tram && 
                        t != TransportType.Subway && t != TransportType.Ship && t != TransportType.Ferry &&
                        t != TransportType.Airplane ) {
                        continue; 
                    }
                }
                else continue;

                UnityEngine.Color renderColor = colors[i].m_Color;
                
                // 1. Draw the Route Lines
                DynamicBuffer<RouteSegment> segments = segmentAccess[i];
                for (int j = 0; j < segments.Length; j++)
                {
                    Entity segmentEntity = segments[j].m_Segment;
                    if (PathElementLookup.TryGetBuffer(segmentEntity, out DynamicBuffer<PathElement> path))
                    {
                        for (int k = 0; k < path.Length; k++)
                        {
                            if (CurveLookup.TryGetComponent(path[k].m_Target, out Curve curve))
                            {
                                overlayBuffer.DrawCurve(renderColor, curve.m_Bezier, thickness, new Unity.Mathematics.float2(0, 1));
                            }
                        }
                    }
                }

                // 2. Accumulate the Stations/Stops instead of drawing them immediately
                if (DrawStops && hasWaypoints)
                {
                    DynamicBuffer<RouteWaypoint> waypoints = waypointAccess[i];
                    for (int w = 0; w < waypoints.Length; w++)
                    {
                        Entity waypointEntity = waypoints[w].m_Waypoint;
                        float3 renderPos = float3.zero;
                        bool validPos = false;
                        Entity uniqueKey = Entity.Null;

                        if (ConnectedLookup.TryGetComponent(waypointEntity, out var connected))
                        {
                            Entity physicalStop = connected.m_Connected;
                            if (TransformLookup.TryGetComponent(physicalStop, out Game.Objects.Transform trans))
                            {
                                renderPos = trans.m_Position;
                                validPos = true;
                                uniqueKey = physicalStop; // Use the building as the overlap key
                            }
                        }
                        else if (TransformLookup.TryGetComponent(waypointEntity, out Game.Objects.Transform trans))
                        {
                            renderPos = trans.m_Position;
                            validPos = true;
                            uniqueKey = waypointEntity; // Use the loose waypoint as the overlap key
                        }

                        if (validPos)
                        {
                            StopColors.Add(uniqueKey, renderColor);
                            if (!StopPositions.ContainsKey(uniqueKey))
                            {
                                StopPositions.Add(uniqueKey, renderPos);
                            }
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct DrawTransitStopsJob : IJob
    {
        public OverlayRenderSystem.Buffer overlayBuffer;
        [ReadOnly] public NativeParallelMultiHashMap<Entity, UnityEngine.Color> stopColors;
        [ReadOnly] public NativeHashMap<Entity, float3> stopPositions;
        public float zoomLevel;
        public bool drawStops;

        public void Execute()
        {
            if (!drawStops) return;

            float minZoom = 1600f;
            float maxZoom = 10000f;
            float normalizedZoom = math.clamp((zoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float baseWidth = 5.0f;
            float maxWidth = baseWidth * 10f;
            float thickness = math.lerp(baseWidth, maxWidth, normalizedZoom);

            var keys = stopPositions.GetKeyArray(Allocator.Temp);
            var uniqueColors = new NativeList<UnityEngine.Color>(8, Allocator.Temp);

            for (int i = 0; i < keys.Length; i++)
            {
                Entity stopEntity = keys[i];
                float3 pos = stopPositions[stopEntity];

                // Extract unique colors for this specific stop
                uniqueColors.Clear();
                if (stopColors.TryGetFirstValue(stopEntity, out UnityEngine.Color color, out var it))
                {
                    uniqueColors.Add(color);
                    while (stopColors.TryGetNextValue(out color, ref it))
                    {
                        // De-duplicate (so overlapping loops of the SAME line don't spawn duplicate slices)
                        bool exists = false;
                        for(int c=0; c<uniqueColors.Length; c++) {
                            if (uniqueColors[c].r == color.r && uniqueColors[c].g == color.g && uniqueColors[c].b == color.b) {
                                exists = true; break;
                            }
                        }
                        if (!exists) uniqueColors.Add(color);
                    }
                }

                if (uniqueColors.Length == 0) continue;

                float outerRadius = thickness * 2.5f;
                float innerRadius = thickness * 1.5f;

                // LAYER 1: Base Black Border (Makes it pop against the road/lines)
                overlayBuffer.DrawCircle(new UnityEngine.Color(0f, 0f, 0f, 0.8f), pos, outerRadius + (thickness * 0.4f));

                if (uniqueColors.Length == 1)
                {
                    // Single line at stop -> Solid Ring
                    overlayBuffer.DrawCircle(uniqueColors[0], pos, outerRadius);
                }
                else
                {
                    // Overlapping lines -> Segmented Pie Chart Ring
                    int colorsCount = uniqueColors.Length;
                    float ringCenterRadius = (outerRadius + innerRadius) * 0.4f;
                    float ringWidth = outerRadius - innerRadius;
                    
                    int segmentsPerColor = 10; // Adjusts arc smoothness
                    float anglePerColor = (math.PI * 2f) / colorsCount;

                    for (int c = 0; c < colorsCount; c++)
                    {
                        UnityEngine.Color cColor = uniqueColors[c];
                        float startAngle = c * anglePerColor;
                        float angleStep = anglePerColor / segmentsPerColor;

                        for (int s = 0; s < segmentsPerColor; s++)
                        {
                            float a1 = startAngle + (s * angleStep);
                            float a2 = startAngle + ((s + 1) * angleStep);

                            float3 p1 = pos + new float3(math.cos(a1), 0, math.sin(a1)) * ringCenterRadius;
                            float3 p2 = pos + new float3(math.cos(a2), 0, math.sin(a2)) * ringCenterRadius;

                            // Draw the segmented arc line (Width expanded slightly to eliminate gaps)
                            overlayBuffer.DrawLine(cColor, new Colossal.Mathematics.Line3.Segment(p1, p2), ringWidth * 1.5f); 
                        }
                    }
                }

                // LAYER 3: Inner Black Border (Masks rough inner edges of line segments cleanly)
                overlayBuffer.DrawCircle(new UnityEngine.Color(0f, 0f, 0f, 0.8f), pos, innerRadius + (thickness * 0.2f));

                // LAYER 4: Bright White Center
                overlayBuffer.DrawCircle(new UnityEngine.Color(1f, 1f, 1f, 0.9f), pos, innerRadius);
            }
            
            uniqueColors.Dispose();
            keys.Dispose();
        }
    }
}