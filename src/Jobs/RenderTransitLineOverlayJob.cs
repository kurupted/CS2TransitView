using System;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Routes;
using Game.Pathfind; 
using Unity.Burst;
using Unity.Burst.Intrinsics; // Required for v128
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Colossal.Mathematics; // Required for Bezier4x3 and MathUtils

namespace BetterTransitView.Jobs
{
    // PASS 1: TALLY OVERLAPPING ROUTES
    [BurstCompile]
    public struct TallySharedSegmentsJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle EntityHandle;
        [ReadOnly] public BufferTypeHandle<RouteSegment> SegmentBufferType;
        [ReadOnly] public BufferLookup<PathElement> PathElementLookup;
        [ReadOnly] public ComponentTypeHandle<HiddenRoute> HiddenRouteType;

        public NativeParallelMultiHashMap<Entity, Entity>.ParallelWriter SegmentToRouteMap;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (chunk.Has(ref HiddenRouteType)) return;

            NativeArray<Entity> entities = chunk.GetNativeArray(EntityHandle);
            BufferAccessor<RouteSegment> segmentAccess = chunk.GetBufferAccessor(ref SegmentBufferType);

            for (int i = 0; i < chunk.Count; i++)
            {
                Entity routeEntity = entities[i];
                DynamicBuffer<RouteSegment> segments = segmentAccess[i];

                // Loop through the segments of the route
                for (int j = 0; j < segments.Length; j++)
                {
                    Entity segmentEntity = segments[j].m_Segment;
                    
                    // Look up the path elements on each segment
                    if (PathElementLookup.TryGetBuffer(segmentEntity, out DynamicBuffer<PathElement> path))
                    {
                        for (int p = 0; p < path.Length; p++)
                        {
                            Entity targetElement = path[p].m_Target; // The actual road curve entity
                            SegmentToRouteMap.Add(targetElement, routeEntity);
                        }
                    }
                }
            }
        }
    }

    // PASS 2: RENDER THE ROUTES
    [BurstCompile]
    public struct RenderTransitLineOverlayJob : IJobChunk
    {
        public OverlayRenderSystem.Buffer overlayBuffer; 
        
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public ComponentTypeHandle<Game.Routes.Color> ColorType;
        [ReadOnly] public BufferTypeHandle<RouteSegment> SegmentBufferType;
        
        [ReadOnly] public BufferTypeHandle<RouteWaypoint> WaypointBufferType;
        [ReadOnly] public ComponentLookup<Game.Routes.Connected> ConnectedLookup; 
        [ReadOnly] public ComponentLookup<Game.Routes.TransportStop> TransportStopLookup;
        [ReadOnly] public ComponentLookup<Game.Objects.Transform> TransformLookup;
        [ReadOnly] public ComponentLookup<Game.Routes.Position> PositionLookup;
        public bool DrawStops;
        
        [ReadOnly] public BufferLookup<PathElement> PathElementLookup;
        [ReadOnly] public ComponentLookup<Curve> CurveLookup;
        [ReadOnly] public NativeHashSet<Entity> HiddenRoutes;

        [ReadOnly] public ComponentLookup<PrefabRef> PrefabRefLookup;
        [ReadOnly] public ComponentLookup<TransportLineData> TransportLineDataLookup;
        public float ZoomLevel; 
        
        // --- Output Containers ---
        public NativeParallelMultiHashMap<Entity, UnityEngine.Color> StopColors;
        public NativeHashMap<Entity, float3> StopPositions;

        public NativeParallelMultiHashMap<Entity, UnityEngine.Color> WaypointColors;
        public NativeHashMap<Entity, float3> WaypointPositions;
        
        [ReadOnly] public NativeParallelMultiHashMap<Entity, Entity> SharedSegmentsMap;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
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
            
            // Set ribbon width slightly narrower than thickness so they snug up together nicely
            float ribbonWidth = thickness * 0.85f; 

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
                            Entity targetElement = path[k].m_Target;
                            if (CurveLookup.TryGetComponent(targetElement, out Curve curveComponent))
                            {
                                Bezier4x3 myCurve = curveComponent.m_Bezier;
                                
                                // --- RIBBON MATH ---
                                Unity.Collections.FixedList512Bytes<Entity> uniqueRoutes = new Unity.Collections.FixedList512Bytes<Entity>();
                                
                                if (SharedSegmentsMap.TryGetFirstValue(targetElement, out Entity routeOnSegment, out var iterator))
                                {
                                    do
                                    {
                                        bool exists = false;
                                        for (int u = 0; u < uniqueRoutes.Length; u++) {
                                            if (uniqueRoutes[u] == routeOnSegment) { exists = true; break; }
                                        }
                                        if (!exists) uniqueRoutes.Add(routeOnSegment);
                                        
                                    } while (SharedSegmentsMap.TryGetNextValue(out routeOnSegment, ref iterator));
                                }

                                int totalLines = uniqueRoutes.Length;

                                // Dynamic thickness scaling!
                                float scaleFactor = 1.0f;
                                if (totalLines > 1) 
                                {
                                    // 2 lines = 70%, 3 lines = even less, capped at a minimum of 35% thickness
                                    scaleFactor = math.max(0.35f, 1.0f - ((totalLines - 1) * 0.30f)); 
                                }
                                
                                // Scale both the visual thickness and the mathematical offset width
                                float currentThickness = thickness * scaleFactor;
                                float currentRibbonWidth = ribbonWidth * scaleFactor;

                                if (totalLines > 1)
                                {
                                    int myIndex = 0;
                                    for (int u = 0; u < totalLines; u++)
                                    {
                                        if (uniqueRoutes[u].Index < routeEntity.Index) 
                                        {
                                            myIndex++;
                                        }
                                    }

                                    // Use the dynamically scaled ribbon width so they stay snug
                                    float offsetAmount = (myIndex - (totalLines - 1) / 2f) * currentRibbonWidth;
                                    float3 tangentA = MathUtils.Tangent(myCurve, 0f);
                                    float3 tangentD = MathUtils.Tangent(myCurve, 1f);
                                    float3 up = new float3(0, 1, 0);
                                    float3 rightA = math.normalizesafe(math.cross(up, tangentA));
                                    float3 rightD = math.normalizesafe(math.cross(up, tangentD));
                                    float3 rightMid = math.normalizesafe(rightA + rightD);

                                    myCurve.a += rightA * offsetAmount;
                                    myCurve.b += rightMid * offsetAmount;
                                    myCurve.c += rightMid * offsetAmount;
                                    myCurve.d += rightD * offsetAmount;
                                }
                                // --- END RIBBON MATH ---

                                overlayBuffer.DrawCurve(renderColor, myCurve, currentThickness, new Unity.Mathematics.float2(0, 1));
                            }
                        }
                    }
                }

                // 2. Accumulate the Stations/Stops/Waypoints
                if (DrawStops && hasWaypoints)
                {
                    DynamicBuffer<RouteWaypoint> waypoints = waypointAccess[i];
                    for (int w = 0; w < waypoints.Length; w++)
                    {
                        Entity waypointEntity = waypoints[w].m_Waypoint;
                        float3 renderPos = float3.zero;
                        bool validPos = false;
                        Entity uniqueKey = waypointEntity; 

                        bool isStop = TransportStopLookup.HasComponent(waypointEntity);
                        bool hasConnected = ConnectedLookup.TryGetComponent(waypointEntity, out var connected);

                        if (hasConnected && TransportStopLookup.HasComponent(connected.m_Connected))
                        {
                            isStop = true;
                            uniqueKey = connected.m_Connected; 
                        }

                        // Get the physical position
                        if (isStop && hasConnected && TransformLookup.TryGetComponent(connected.m_Connected, out Game.Objects.Transform stopTrans))
                        {
                            // Actual transit stops have a physical Transform
                            renderPos = stopTrans.m_Position;
                            validPos = true;
                        }
                        else if (PositionLookup.TryGetComponent(waypointEntity, out Game.Routes.Position wpPos))
                        {
                            renderPos = wpPos.m_Position;
                            validPos = true;
                        }

                        if (validPos)
                        {
                            if (isStop) 
                            {
                                StopColors.Add(uniqueKey, renderColor);
                                if (!StopPositions.ContainsKey(uniqueKey)) StopPositions.Add(uniqueKey, renderPos);
                            }
                            else 
                            {
                                WaypointColors.Add(uniqueKey, renderColor);
                                if (!WaypointPositions.ContainsKey(uniqueKey)) WaypointPositions.Add(uniqueKey, renderPos);
                            }
                        }
                    }
                }
            }
        }
    }

    // PASS 3: DRAW TRANSIT STOPS
    [BurstCompile]
    public struct DrawTransitStopsJob : IJob
    {
        public OverlayRenderSystem.Buffer overlayBuffer;
        [ReadOnly] public NativeParallelMultiHashMap<Entity, UnityEngine.Color> stopColors;
        [ReadOnly] public NativeHashMap<Entity, float3> stopPositions;
        public float zoomLevel;
        public bool drawStops;
        public bool showWaiting;
        
        // Using camera vectors so the labels float and face the camera
        public float3 cameraRight; 
        public float3 cameraUp;
        public float3 cameraPosition; 

        [ReadOnly] public BufferLookup<Game.Routes.ConnectedRoute> ConnectedRouteLookup;
        [ReadOnly] public ComponentLookup<Game.Routes.WaitingPassengers> WaitingPassengersLookup;
        [ReadOnly] public ComponentLookup<Game.Common.Owner> OwnerLookup;
        [ReadOnly] public ComponentLookup<Game.Routes.Color> ColorLookup;
        [ReadOnly] public NativeHashSet<Entity> HiddenRoutes;

        private struct LabelData
        {
            public float3 originalPos;
            public int count;
            public int waitTime; 
            public UnityEngine.Color bgColor;
            public UnityEngine.Color textColor;
            public float sortScore;
            public float outerRadius; 
            public int side; // 1 for right, -1 for left
        }

        private struct LabelComparer : System.Collections.Generic.IComparer<LabelData>
        {
            public int Compare(LabelData x, LabelData y)
            {
                return x.sortScore.CompareTo(y.sortScore);
            }
        }

        private struct DrawnLabel
        {
            public float3 center;
            public float width;
        }

        public void Execute()
        {
            if (!drawStops) return;

            float minZoom = 1000f;
            float maxZoom = 14000f;
            float rawZoom = math.clamp((zoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float normalizedZoom = math.pow(rawZoom, 0.7f); 
            
            float baseWidth = 4.5f;
            
            float stopThickness = math.lerp(baseWidth, baseWidth * 11.0f, normalizedZoom); // circles
            float labelThickness = math.lerp(baseWidth, baseWidth * 7.0f, normalizedZoom); // labels

            var keys = stopPositions.GetKeyArray(Allocator.Temp);
            var uniqueColors = new NativeList<UnityEngine.Color>(8, Allocator.Temp);
            NativeList<LabelData> pendingLabels = new NativeList<LabelData>(keys.Length, Allocator.Temp);

            // PASS 1: Draw the Stop Circles
            for (int i = 0; i < keys.Length; i++)
            {
                Entity stopEntity = keys[i];
                float3 pos = stopPositions[stopEntity] + new float3(0f, 1.2f, 0f);

                // Alternating side logic! (1 = Right, -1 = Left)
                int sideMultiplier = (stopEntity.Index % 2 == 0) ? 1 : -1;

                // Extract unique colors for this specific stop
                uniqueColors.Clear();
                if (stopColors.TryGetFirstValue(stopEntity, out UnityEngine.Color color, out var it))
                {
                    uniqueColors.Add(color);
                    while (stopColors.TryGetNextValue(out color, ref it))
                    {
                        // De-duplicate
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

                float outerRadius = stopThickness * 2.5f;
                float innerRadius = stopThickness * 1.5f;

                // Base Black Border
                overlayBuffer.DrawCircle(new UnityEngine.Color(0f, 0f, 0f, 0.8f), pos, outerRadius + (stopThickness * 0.4f));

                if (uniqueColors.Length == 1)
                {
                    overlayBuffer.DrawCircle(uniqueColors[0], pos, outerRadius);
                }
                else
                {
                    int colorsCount = uniqueColors.Length;
                    float ringCenterRadius = (outerRadius + innerRadius) * 0.5f;
                    float ringWidth = (outerRadius - innerRadius) * 1.2f; // size multiplier
                    
                    int segmentsPerColor = 10;
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

                            overlayBuffer.DrawLine(cColor, new Colossal.Mathematics.Line3.Segment(p1, p2), ringWidth); 
                        }
                    }
                }

                // Inner Black Border
                overlayBuffer.DrawCircle(new UnityEngine.Color(0f, 0f, 0f, 0.8f), pos, innerRadius + (stopThickness * 0.2f));
                // Bright White Center
                overlayBuffer.DrawCircle(new UnityEngine.Color(1f, 1f, 1f, 0.9f), pos, innerRadius);

                // GATHER STATS
                if (showWaiting && normalizedZoom < 0.6f)
                {
                    if (ConnectedRouteLookup.TryGetBuffer(stopEntity, out var connectedRoutes))
                    {
                        for (int c = 0; c < connectedRoutes.Length; c++)
                        {
                            Entity waypoint = connectedRoutes[c].m_Waypoint;
                            if (WaitingPassengersLookup.TryGetComponent(waypoint, out var passengers) && passengers.m_Count > 0)
                            {
                                if (OwnerLookup.TryGetComponent(waypoint, out var owner))
                                {
                                    Entity routeEntity = owner.m_Owner;
                                    if (!HiddenRoutes.Contains(routeEntity) && ColorLookup.TryGetComponent(routeEntity, out var routeColor))
                                    {
                                        AddPendingLabel(ref pendingLabels, pos, passengers.m_Count, passengers.m_AverageWaitingTime, routeColor.m_Color, outerRadius, sideMultiplier);
                                    }
                                }
                            }
                        }
                    }
                    else if (WaitingPassengersLookup.TryGetComponent(stopEntity, out var passengers) && passengers.m_Count > 0)
                    {
                        if (OwnerLookup.TryGetComponent(stopEntity, out var owner))
                        {
                            Entity routeEntity = owner.m_Owner;
                            if (!HiddenRoutes.Contains(routeEntity) && ColorLookup.TryGetComponent(routeEntity, out var routeColor))
                            {
                                AddPendingLabel(ref pendingLabels, pos, passengers.m_Count, passengers.m_AverageWaitingTime, routeColor.m_Color, outerRadius, sideMultiplier);
                            }
                        }
                    }
                }
            }

            // PASS 2: Sort and Draw Labels
            pendingLabels.Sort(new LabelComparer());
            NativeList<DrawnLabel> drawnLabels = new NativeList<DrawnLabel>(pendingLabels.Length, Allocator.Temp);

            float shiftAmount = labelThickness * 4.8f; 
            float horizontalGap = labelThickness * 14.0f; // Significantly push labels away from the stop

            for (int i = 0; i < pendingLabels.Length; i++)
            {
                LabelData label = pendingLabels[i];
                float3 toCam = math.normalizesafe(cameraPosition - label.originalPos);
                
                float bgWidth = GetLabelWidth(label.count, label.waitTime, labelThickness);
                
                // Position base is now offset massively to either Left (-1) or Right (+1) based on the label.side
                float3 indicatorPos = label.originalPos + (cameraRight * (label.side * horizontalGap)) + (toCam * 25.0f);
                
                int maxStack = 20; 
                for (int stack = 0; stack < maxStack; stack++)
                {
                    bool overlap = false;
                    for (int d = 0; d < drawnLabels.Length; d++)
                    {
                        DrawnLabel other = drawnLabels[d];
                        
                        float3 diff = indicatorPos - other.center;
                        float distRight = math.abs(math.dot(diff, cameraRight));
                        float distUp = math.abs(math.dot(diff, cameraUp));
                        
                        float minRightDist = (bgWidth * 0.5f) + (other.width * 0.5f) + (labelThickness * 1.0f); 
                        float minUpDist = labelThickness * 4.5f; 
                        
                        if (distRight < minRightDist && distUp < minUpDist)
                        {
                            overlap = true;
                            break;
                        }
                    }
                    if (!overlap) break; 
                    indicatorPos += (cameraUp * shiftAmount); 
                }
                
                drawnLabels.Add(new DrawnLabel { center = indicatorPos, width = bgWidth });

                // CONNECTOR LINE MATH
                // 1. Calculate the center of the stop (in camera projection space)
                float3 stopCenterCam = label.originalPos + (toCam * 25.0f);
                
                // 2. Identify the inner edge of the label pill we are connecting to
                float3 pillEdge = indicatorPos - (cameraRight * (label.side * bgWidth * 0.5f));
                
                // 3. Aim from the center towards the pill edge
                float3 vectorToEdge = pillEdge - stopCenterCam;
                float distToEdge = math.length(vectorToEdge);
                
                if (distToEdge > 0.001f)
                {
                    float3 dir = vectorToEdge / distToEdge;
                    
                    // 4. Start drawing outside the circle radius (1.3x so it doesn't bleed inside)
                    float startOffset = label.outerRadius * 1.3f;
                    
                    if (distToEdge > startOffset) // Ensure we have room to draw a line
                    {
                        float3 lineStart = stopCenterCam + (dir * startOffset);
                        overlayBuffer.DrawLine(label.bgColor, new Colossal.Mathematics.Line3.Segment(lineStart, pillEdge), labelThickness * 0.4f);
                    }
                }

                DrawLabelIndicator(indicatorPos, label.count, label.waitTime, label.bgColor, label.textColor, labelThickness, cameraRight, cameraUp);
            }
            
            drawnLabels.Dispose();
            pendingLabels.Dispose();
            uniqueColors.Dispose();
            keys.Dispose();
        }

        private float GetCharWidth(char c, float baseDigitWidth)
        {
            if (c == ' ') return baseDigitWidth * 0.4f;
            if (c == '-') return baseDigitWidth * 0.5f;
            if (c == 'm') return baseDigitWidth * 1.1f; // 'm' is slightly wider than a standard number
            return baseDigitWidth;
        }

        private float GetLabelWidth(int count, int waitTime, float labelThickness)
        {
            int tempCount = count;
            int countDigits = 0;
            if (tempCount == 0) countDigits = 1;
            while (tempCount > 0) { countDigits++; tempCount /= 10; }

            int tempWait = waitTime;
            int waitDigits = 0;
            if (tempWait == 0) waitDigits = 1;
            while (tempWait > 0) { waitDigits++; tempWait /= 10; }

            float digitWidth = labelThickness * 1.2f;
            float spacing = labelThickness * 0.6f;
            
            float spaceW = GetCharWidth(' ', digitWidth);
            float hyphenW = GetCharWidth('-', digitWidth);
            float mW = GetCharWidth('m', digitWidth);

            float totalWidth = (countDigits * digitWidth) + spaceW + hyphenW + spaceW + (waitDigits * digitWidth) + mW;
            int totalChars = countDigits + 3 + waitDigits + 1; // 3 chars for " - ", 1 for "m"
            totalWidth += (totalChars - 1) * spacing;

            float horizontalPadding = labelThickness * 3.5f; 
            return totalWidth + horizontalPadding;
        }

        private void AddPendingLabel(ref NativeList<LabelData> list, float3 pos, int count, int wait, UnityEngine.Color bgColor, float outerRadius, int side)
        {
            float luminance = (bgColor.r * 0.299f) + (bgColor.g * 0.587f) + (bgColor.b * 0.114f);
            UnityEngine.Color textColor = luminance > 0.5f ? new UnityEngine.Color(0.1f, 0.1f, 0.1f, 1f) : new UnityEngine.Color(1f, 1f, 1f, 1f);
            float score = math.dot(pos, cameraUp);

            list.Add(new LabelData {
                originalPos = pos,
                count = count,
                waitTime = wait, 
                bgColor = bgColor,
                textColor = textColor,
                sortScore = score,
                outerRadius = outerRadius,
                side = side
            });
        }

        private void DrawLabelIndicator(float3 center, int count, int waitTime, UnityEngine.Color bgColor, UnityEngine.Color textColor, float thickness, float3 right, float3 up)
        {
            NativeList<char> chars = new NativeList<char>(16, Allocator.Temp);

            int tempNum = count;
            if (tempNum == 0) chars.Add('0');
            else
            {
                NativeList<char> rev = new NativeList<char>(8, Allocator.Temp);
                while (tempNum > 0)
                {
                    rev.Add((char)('0' + (tempNum % 10)));
                    tempNum /= 10;
                }
                for (int i = rev.Length - 1; i >= 0; i--) chars.Add(rev[i]);
            }

            chars.Add(' '); chars.Add('-'); chars.Add(' ');

            int tempWait = waitTime;
            if (tempWait == 0) chars.Add('0');
            else
            {
                NativeList<char> rev = new NativeList<char>(8, Allocator.Temp);
                while (tempWait > 0)
                {
                    rev.Add((char)('0' + (tempWait % 10)));
                    tempWait /= 10;
                }
                for (int i = rev.Length - 1; i >= 0; i--) chars.Add(rev[i]);
            }

            chars.Add('m');

            float digitWidth = thickness * 1.2f;
            float digitHeight = thickness * 2.2f;
            float spacing = thickness * 0.6f;
            float lineThickness = thickness * 0.4f;

            // Calculate total dynamic width for the background pill
            float totalWidth = 0f;
            for (int i = 0; i < chars.Length; i++)
            {
                totalWidth += GetCharWidth(chars[i], digitWidth);
                if (i < chars.Length - 1) totalWidth += spacing;
            }

            float horizontalPadding = thickness * 3.5f; 
            float bgWidth = totalWidth + horizontalPadding;
            
            float3 bgStart = center - (right * (bgWidth * 0.5f));
            float3 bgEnd = center + (right * (bgWidth * 0.5f));
            
            // Draw background pill
            overlayBuffer.DrawLine(bgColor, new Colossal.Mathematics.Line3.Segment(bgStart, bgEnd), digitHeight + (thickness * 2.5f));
            
            // Start the text rendering cursor at the far left edge of the text block
            float3 cursor = center - (right * (totalWidth * 0.5f));

            for (int i = 0; i < chars.Length; i++)
            {
                float charW = GetCharWidth(chars[i], digitWidth);
                
                // Since the character is drawn from its center point, we shift the cursor by half its width
                float3 charCenter = cursor + (right * (charW * 0.5f));
                DrawChar(charCenter, chars[i], textColor, charW, digitHeight, lineThickness, right, up);
                
                // Advance the cursor to the end of this character, plus the spacing gap
                cursor += (right * (charW + spacing));
            }

            chars.Dispose();
        }

        private void DrawChar(float3 center, char c, UnityEngine.Color color, float w, float h, float thickness, float3 right, float3 up)
        {
            if (c == ' ') return;

            float hw = w * 0.5f;
            float hh = h * 0.5f;

            if (c == 'm')
            {
                float3 c_ml = center - (right * hw);
                float3 c_mr = center + (right * hw);
                float3 c_bl = center - (right * hw) - (up * hh);
                float3 c_br = center + (right * hw) - (up * hh);
                float3 c_bm = center - (up * hh);
                
                // Left leg (bottom to middle)
                overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(c_bl, c_ml), thickness); 
                // Middle leg (bottom to middle)
                overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(c_bm, center), thickness); 
                // Right leg (bottom to middle)
                overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(c_br, c_mr), thickness); 
                // Left top curve (left to center)
                overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(c_ml, center), thickness); 
                // Right top curve (center to right)
                overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(center, c_mr), thickness); 
                return;
            }

            byte mask = GetCharMask(c);

            float3 tl = center - (right * hw) + (up * hh);
            float3 tr = center + (right * hw) + (up * hh);
            float3 ml = center - (right * hw);
            float3 mr = center + (right * hw);
            float3 bl = center - (right * hw) - (up * hh);
            float3 br = center + (right * hw) - (up * hh);
            float3 tm = center + (up * hh);
            float3 bm = center - (up * hh);

            float3 i_x = right * (thickness * 0.2f);
            float3 i_y = up * (thickness * 0.2f);

            if ((mask & 1) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(tl+i_x, tr-i_x), thickness); 
            if ((mask & 2) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(tr-i_y, mr+i_y), thickness); 
            if ((mask & 4) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(mr-i_y, br+i_y), thickness); 
            if ((mask & 8) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(br-i_x, bl+i_x), thickness); 
            if ((mask & 16) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(bl+i_y, ml-i_y), thickness); 
            if ((mask & 32) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(ml+i_y, tl-i_y), thickness); 
            if ((mask & 64) != 0) overlayBuffer.DrawLine(color, new Colossal.Mathematics.Line3.Segment(ml+i_x, mr-i_x), thickness); 
        }

        private byte GetCharMask(char c)
        {
            switch (c)
            {
                case '0': return 0x3F; case '1': return 0x06; case '2': return 0x5B; case '3': return 0x4F;
                case '4': return 0x66; case '5': return 0x6D; case '6': return 0x7D; case '7': return 0x07;
                case '8': return 0x7F; case '9': return 0x6F;
                case '-': return 0x40;
                default: return 0;
            }
        }
    }
    
    
    // PASS 4: DRAW WAYPOINTS
    [BurstCompile]
    public struct DrawTransitWaypointsJob : IJob
    {
        public OverlayRenderSystem.Buffer overlayBuffer;
        [ReadOnly] public NativeParallelMultiHashMap<Entity, UnityEngine.Color> waypointColors;
        [ReadOnly] public NativeHashMap<Entity, float3> waypointPositions;
        public float zoomLevel;

        public void Execute()
        {
            var keys = waypointPositions.GetKeyArray(Allocator.Temp);
            if (keys.Length == 0) 
            {
                keys.Dispose();
                return;
            }
            keys.Sort(); 

            float minZoom = 1600f;
            float maxZoom = 10000f;
            float normalizedZoom = math.clamp((zoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float thickness = math.lerp(4.0f, 4.0f * 12f, normalizedZoom);
            float radius = thickness * 2.1f;

            for (int i = 0; i < keys.Length; i++)
            {
                Entity entity = keys[i];
                float3 pos = waypointPositions[entity];

                if (waypointColors.TryGetFirstValue(entity, out UnityEngine.Color color, out _))
                {
                    overlayBuffer.DrawCircle(new UnityEngine.Color(0, 0, 0, 0.7f), pos, radius + (thickness * 0.3f));
                    overlayBuffer.DrawCircle(color, pos, radius); 
                }
            }
            keys.Dispose();
        }
    }
    
    

    
    [BurstCompile]
    public struct DrawTransitVehiclesJob : IJobChunk
    {
        public OverlayRenderSystem.Buffer overlayBuffer;
        [ReadOnly] public EntityTypeHandle EntityType;
        [ReadOnly] public BufferTypeHandle<RouteVehicle> RouteVehicleBufferType;
        [ReadOnly] public ComponentTypeHandle<Game.Routes.Color> ColorType;
        
        [ReadOnly] public ComponentLookup<Game.Rendering.InterpolatedTransform> InterpolatedTransformLookup;
        [ReadOnly] public ComponentLookup<Game.Objects.Transform> TransformLookup;
        
        [ReadOnly] public NativeHashSet<Entity> HiddenRoutes;
        public float ZoomLevel;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!chunk.Has(ref RouteVehicleBufferType)) return;

            NativeArray<Entity> entities = chunk.GetNativeArray(EntityType);
            NativeArray<Game.Routes.Color> colors = chunk.GetNativeArray(ref ColorType);
            BufferAccessor<RouteVehicle> vehicleAccess = chunk.GetBufferAccessor(ref RouteVehicleBufferType);

            float minZoom = 1000f;
            float maxZoom = 14000f;
            float rawZoom = math.clamp((ZoomLevel - minZoom) / (maxZoom - minZoom), 0f, 1f);
            float normalizedZoom = math.pow(rawZoom, 0.7f); 
            
            float width = math.lerp(6.0f, 20.0f, normalizedZoom); 
            float halfLength = width * 1.5f; 

            for (int i = 0; i < chunk.Count; i++)
            {
                Entity routeEntity = entities[i];
                if (HiddenRoutes.Contains(routeEntity)) continue;

                UnityEngine.Color routeColor = colors[i].m_Color;
                DynamicBuffer<RouteVehicle> vehicles = vehicleAccess[i];

                for (int v = 0; v < vehicles.Length; v++)
                {
                    Entity vehicleEntity = vehicles[v].m_Vehicle;
                    float3 pos;
                    Unity.Mathematics.quaternion rot;

                    if (InterpolatedTransformLookup.TryGetComponent(vehicleEntity, out var iTransform))
                    {
                        pos = iTransform.m_Position;
                        rot = iTransform.m_Rotation;
                    }
                    else if (TransformLookup.TryGetComponent(vehicleEntity, out var transform))
                    {
                        pos = transform.m_Position;
                        rot = transform.m_Rotation;
                    }
                    else continue;

                    float3 forward = math.mul(rot, new float3(0, 0, 1));
                    float3 front = pos + (forward * halfLength);
                    float3 back = pos - (forward * halfLength);

                    float3 verticalOffset = new float3(0f, 0.9f, 0f);
                    float3 frontRaised = front + verticalOffset;
                    float3 backRaised = back + verticalOffset;

                    // Stretch the outline line slightly forward and backward so it adds end-caps (border on all 4 sides)
                    float outlineExtra = width * 0.15f; 
                    float3 frontOutline = frontRaised + (forward * outlineExtra);
                    float3 backOutline = backRaised - (forward * outlineExtra);

                    overlayBuffer.DrawLine(new UnityEngine.Color(1f, 1f, 1f, 1f), new Colossal.Mathematics.Line3.Segment(backOutline, frontOutline), width + (width * 0.3f));

                    float3 bodyOffset = new float3(0f, 0.15f, 0f);
                    overlayBuffer.DrawLine(routeColor, new Colossal.Mathematics.Line3.Segment(backRaised + bodyOffset, frontRaised + bodyOffset), width);
                }
            }
        }
    }
    
}