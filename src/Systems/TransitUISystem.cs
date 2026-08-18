using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Common;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace BetterTransitView.Systems
{
    public enum BetterTransitViewStatusType
    {
        Stations = 101
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TransitUISystem : InfoSectionBase
    {
        private ToolSystem m_ToolSystem;
        private Game.UI.InGame.InfoviewsUISystem m_InfoviewsUISystem;
        private Game.UI.InGame.SelectedInfoUISystem m_SelectedInfoUISystem;

        // UI Bindings
        private ValueBinding<bool> showTransitPanelBinding;
        private ValueBinding<string> transitLinesDataBinding;
        private ValueBinding<bool> showStopsAndStationsBinding;
        private ValueBinding<bool> showWaitingPassengersBinding;
        private ValueBinding<bool> showTransitVehiclesBinding;
        private ValueBinding<bool> showInfoviewBackgroundBinding;
        private ValueBinding<int> selectedTransitLineBinding;
        private ValueBinding<bool> isMapPickerActiveBinding;

        // Queries & Entities
        private EntityQuery m_TransitLinesQuery;
        private EntityQuery m_TransportLinePrefabQuery;
        private Game.Prefabs.InfoviewPrefab m_CustomInfoview;
        private Entity m_CustomInfoviewEntity = Entity.Null;
        private EntityQuery m_ActiveInfomodeQuery;
        
        // State
        public bool IsTransitPanelActive => this.showTransitPanelBinding?.value ?? false;
        private string m_ActiveTransitMode = "none";
        private string m_PendingTransitMode = "none";
        private bool m_ModeChangeRequested = false;
        private bool m_IsMapPickerActive = false;
        private int m_TransitUpdateFrame = 0;
        private bool m_TransitLinesDirty = false;
        private bool m_WasToggleKeyDown = false;
        private int m_EnforceInfoviewFrames = 0;
        private Game.Tools.ToolBaseSystem m_LastActiveTool;
        private bool m_HasInitializedDefaults = false;
        private HashSet<Entity> m_SavedHiddenRoutes = new HashSet<Entity>();

        // Public Statics for the Render Jobs
        public static HashSet<Entity> HiddenCustomRoutes = new HashSet<Entity>();
        public static bool ShowStopsAndStations = true; 
        public static bool ShowWaitingPassengers = false;
        public static bool ShowTransitVehicles = false;
        public static bool ShowInfoviewBackground = false; 

        protected override void OnCreate()
        {
            base.OnCreate();
            
            // Register this as a UI section
            m_InfoUISystem.AddMiddleSection(this);

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_InfoviewsUISystem = World.GetOrCreateSystemManaged<Game.UI.InGame.InfoviewsUISystem>();
            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<Game.UI.InGame.SelectedInfoUISystem>();

            SetupCustomInfoview();

            // Setup Queries
            m_TransitLinesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Route>(),
                    ComponentType.ReadOnly<TransportLine>(),
                    ComponentType.ReadOnly<Game.Prefabs.PrefabRef>()
                },
                None = new ComponentType[] {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            m_TransportLinePrefabQuery = GetEntityQuery(new EntityQueryDesc {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Prefabs.TransportLineData>(),
                    ComponentType.ReadOnly<Game.Prefabs.PrefabData>()
                }
            });
            
            m_ActiveInfomodeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {
                    ComponentType.ReadOnly<Game.Prefabs.InfoviewBuildingStatusData>(),
                    ComponentType.ReadOnly<Game.Prefabs.InfomodeActive>()
                }
            });

            // Initialize Bindings
            this.showTransitPanelBinding = new ValueBinding<bool>("BetterTransitView", "showTransitPanel", false);
            this.transitLinesDataBinding = new ValueBinding<string>("BetterTransitView", "transitLinesData", "[]");
            this.showStopsAndStationsBinding = new ValueBinding<bool>("BetterTransitView", "showStopsAndStations", true);
            ShowInfoviewBackground = BetterTransitView.ModSettings.ModSettings.Instance.MapModeActivatedByDefault;
            this.showInfoviewBackgroundBinding = new ValueBinding<bool>("BetterTransitView", "showInfoviewBackground", ShowInfoviewBackground);
            this.showWaitingPassengersBinding = new ValueBinding<bool>("BetterTransitView", "showWaitingPassengers", ShowWaitingPassengers);
            this.showTransitVehiclesBinding = new ValueBinding<bool>("BetterTransitView", "showTransitVehicles", ShowTransitVehicles);
            this.selectedTransitLineBinding = new ValueBinding<int>("BetterTransitView", "selectedTransitLine", 0);
            this.isMapPickerActiveBinding = new ValueBinding<bool>("BetterTransitView", "isMapPickerActive", false);
            
            AddBinding(this.showTransitPanelBinding);
            AddBinding(this.transitLinesDataBinding);
            AddBinding(this.showStopsAndStationsBinding);
            AddBinding(this.showWaitingPassengersBinding);
            AddBinding(this.showTransitVehiclesBinding);
            AddBinding(this.showInfoviewBackgroundBinding);
            AddBinding(this.selectedTransitLineBinding);
            AddBinding(this.isMapPickerActiveBinding);

            // Mock data for initial UI render safety
            this.transitLinesDataBinding.Update("[]");

            // --- Triggers ---

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "toggleTransitCustom", (active) => {
                m_PendingTransitMode = active ? "custom" : "none";
                m_ModeChangeRequested = true;
            }));

            AddBinding(new TriggerBinding<int, bool>("BetterTransitView", "setLineVisible", (entityIndex, show) => {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in entities)
                {
                    if (e.Index == entityIndex)
                    {
                        if (show) HiddenCustomRoutes.Remove(e);
                        else HiddenCustomRoutes.Add(e);

                        m_TransitLinesDirty = true;
                        break;
                    }
                }
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setAllLinesVisible", (show) => {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in entities)
                {
                    if (show) HiddenCustomRoutes.Remove(e);
                    else HiddenCustomRoutes.Add(e);
                }
                m_TransitLinesDirty = true;
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowStopsAndStations", (show) => {
                ShowStopsAndStations = show;
                this.showStopsAndStationsBinding.Update(show);
            }));
            
            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowWaitingPassengers", (show) => {
                ShowWaitingPassengers = show;
                this.showWaitingPassengersBinding.Update(show);
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowTransitVehicles", (show) => {
                ShowTransitVehicles = show;
                this.showTransitVehiclesBinding.Update(show);
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowInfoviewBackground", (show) => {
                ShowInfoviewBackground = show;
                this.showInfoviewBackgroundBinding.Update(show);
                BetterTransitView.ModSettings.ModSettings.Instance.MapModeActivatedByDefault = show;
                BetterTransitView.ModSettings.ModSettings.Instance.Apply();
    
                if (this.IsTransitPanelActive) {
                    if (show && m_CustomInfoviewEntity != Entity.Null) {
                        m_InfoviewsUISystem.SetActiveInfoview(m_CustomInfoviewEntity);
                    } else {
                        m_InfoviewsUISystem.SetActiveInfoview(Entity.Null);
                    }
                }
            }));

            AddBinding(new TriggerBinding("BetterTransitView", "handleEscapeKey", () => {
                TryCloseOnEscape();
            }));

            AddBinding(new TriggerBinding<string>("BetterTransitView", "activateTransitTool", (mode) => {
                Game.Prefabs.TransportType targetType = Game.Prefabs.TransportType.Bus;
                bool isCargo = false;
    
                switch(mode.ToLower()) {
                    case "bus": targetType = Game.Prefabs.TransportType.Bus; break;
                    case "train": targetType = Game.Prefabs.TransportType.Train; break;
                    case "tram": targetType = Game.Prefabs.TransportType.Tram; break;
                    case "subway": targetType = Game.Prefabs.TransportType.Subway; break;
                    case "ferry": targetType = Game.Prefabs.TransportType.Ferry; break;
                    case "ship": targetType = Game.Prefabs.TransportType.Ship; break;
                    case "airplane": targetType = Game.Prefabs.TransportType.Airplane; break;
                    case "cargo": targetType = Game.Prefabs.TransportType.Train; isCargo = true; break; 
                }

                var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                using var entities = m_TransportLinePrefabQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Game.Prefabs.PrefabBase selectedPrefab = null;

                foreach(var e in entities) {
                    if (EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(e, out var lineData)) {
                        if (lineData.m_TransportType == targetType && lineData.m_CargoTransport == isCargo) {
                            selectedPrefab = prefabSystem.GetPrefab<Game.Prefabs.PrefabBase>(e);
                            break;
                        }
                    }
                }

                if (selectedPrefab != null) {
                    m_ToolSystem.ActivatePrefabTool(selectedPrefab);
                    m_EnforceInfoviewFrames = 2;
                }
            }));

            AddBinding(new TriggerBinding<int>("BetterTransitView", "showVanillaLineInfo", (entityIndex) => {
                var connectedLookup = GetComponentLookup<Game.Routes.Connected>(true);
                var transformLookup = GetComponentLookup<Game.Objects.Transform>(true);
                var cameraUpdateSystem = World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();

                // Check if it's a transit line
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in entities)
                {
                    if (e.Index == entityIndex)
                    {
                        m_ToolSystem.selected = e;
                        return;
                    }
                }

                // If it's a stop waypoint or stop building: find 3D position and move camera pivot without opening property window
                Entity targetEntity = Entity.Null;
                foreach (var e in entities)
                {
                    if (EntityManager.TryGetBuffer(e, true, out DynamicBuffer<Game.Routes.RouteWaypoint> waypoints))
                    {
                        foreach (var wp in waypoints)
                        {
                            if (wp.m_Waypoint.Index == entityIndex)
                            {
                                targetEntity = (connectedLookup.TryGetComponent(wp.m_Waypoint, out var conn) && EntityManager.Exists(conn.m_Connected)) 
                                    ? conn.m_Connected 
                                    : wp.m_Waypoint;
                                break;
                            }
                        }
                    }
                    if (targetEntity != Entity.Null) break;
                }

                if (targetEntity == Entity.Null)
                {
                    var allEntities = EntityManager.GetAllEntities(Unity.Collections.Allocator.Temp);
                    for (int i = 0; i < allEntities.Length; i++)
                    {
                        if (allEntities[i].Index == entityIndex)
                        {
                            targetEntity = (connectedLookup.TryGetComponent(allEntities[i], out var conn) && EntityManager.Exists(conn.m_Connected)) 
                                ? conn.m_Connected 
                                : allEntities[i];
                            break;
                        }
                    }
                    allEntities.Dispose();
                }

                if (targetEntity != Entity.Null && transformLookup.TryGetComponent(targetEntity, out var transform))
                {
                    if (cameraUpdateSystem != null && cameraUpdateSystem.activeCameraController != null)
                    {
                        cameraUpdateSystem.activeCameraController.pivot = transform.m_Position;
                    }
                }
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "toggleMapPicker", (active) => {
                m_IsMapPickerActive = active;
                this.isMapPickerActiveBinding.Update(active);
                if (active) m_ToolSystem.activeTool = World.GetOrCreateSystemManaged<BetterTransitViewPickerToolSystem>();
                else m_ToolSystem.activeTool = World.GetOrCreateSystemManaged<Game.Tools.DefaultToolSystem>();
            }));

            AddBinding(new TriggerBinding("BetterTransitView", "resetSelectedTransitLine", () => {
                this.selectedTransitLineBinding.Update(0);
            }));
        }

        protected override string group => "BetterTransitView.Systems.TransitUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        public void CancelMapPicker()
        {
            m_IsMapPickerActive = false;
            this.isMapPickerActiveBinding.Update(false);
        }

        public void OnPickerClicked(Unity.Mathematics.float3 hitPos)
        {
            int closestLine = FindClosestRouteTo(hitPos);
            if (closestLine != 0)
            {
                this.selectedTransitLineBinding.Update(closestLine);
                // Do not update to 0 here! JS will update it back to 0 once it receives the value.
            }
            m_IsMapPickerActive = false;
            this.isMapPickerActiveBinding.Update(false);
        }
        
        public void TryCloseOnEscape()
        {
            if (!this.IsTransitPanelActive) return;

            var defaultTool = World.GetOrCreateSystemManaged<Game.Tools.DefaultToolSystem>();
            bool toolActive = m_ToolSystem.activeTool != defaultTool;
            bool entitySelected = m_SelectedInfoUISystem != null && m_SelectedInfoUISystem.selectedEntity != Entity.Null;

            bool otherInfoviewActive = false;
            if (!m_ActiveInfomodeQuery.IsEmptyIgnoreFilter)
            {
                using var statusDatas = m_ActiveInfomodeQuery.ToComponentDataArray<Game.Prefabs.InfoviewBuildingStatusData>(Unity.Collections.Allocator.Temp);
                foreach (var status in statusDatas)
                {
                    if ((int)status.m_Type != (int)BetterTransitViewStatusType.Stations)
                    {
                        otherInfoviewActive = true;
                        break;
                    }
                }
            }

            if (!toolActive && !entitySelected && !otherInfoviewActive)
            {
                DeactivateTransitMode();
            }
        }
        
        protected override void OnUpdate()
        {
            if (!Enabled) Enabled = true;

            // 1. Check for Hotkey Press
            if (Mod.m_ToggleAction != null)
            {
                bool isPressed = Mod.m_ToggleAction.IsPressed();
                // Only trigger if pressed NOW but wasn't pressed LAST frame
                if (isPressed && !m_WasToggleKeyDown)
                {
                    // Toggle the panel based on its current state
                    m_PendingTransitMode = this.IsTransitPanelActive ? "none" : "custom";
                    m_ModeChangeRequested = true;
                }
                m_WasToggleKeyDown = isPressed;
            }

            // Check for Escape key press frame
            if (this.IsTransitPanelActive && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TryCloseOnEscape();
            }

            // 2. Process Mode Changes Safely on the Main Thread
            if (m_ModeChangeRequested)
            {
                if (m_PendingTransitMode == "none") DeactivateTransitMode();
                else ActivateTransitMode(m_PendingTransitMode);
                m_ModeChangeRequested = false;
            }
            
            // Detect if a tool was just equipped OR unequipped
            if (m_ToolSystem.activeTool != m_LastActiveTool)
            {
                m_LastActiveTool = m_ToolSystem.activeTool;
                // If our panel is open, protect our infoview state from vanilla tool-drop behaviors
                if (this.IsTransitPanelActive) 
                {
                    m_EnforceInfoviewFrames = 2;
                }
            }

            // 3. Transit Panel Logic Loop
            if (this.IsTransitPanelActive)
            {
                SyncVanillaVisibilityToUI();

                // Keep the Gray Map checkbox synced if the user manually closes the vanilla infoview
                // But, pause the sync logic if we are actively enforcing the infoview state (ie trying to prevent vanilla view after we click Tool)
                if (m_EnforceInfoviewFrames == 0 && m_CustomInfoviewEntity != Entity.Null)
                {
                    bool isActuallyGray = false;
                    
                    if (!m_ActiveInfomodeQuery.IsEmptyIgnoreFilter)
                    {
                        using var statusDatas = m_ActiveInfomodeQuery.ToComponentDataArray<Game.Prefabs.InfoviewBuildingStatusData>(Unity.Collections.Allocator.Temp);
                        foreach (var status in statusDatas)
                        {
                            if ((int)status.m_Type == (int)BetterTransitViewStatusType.Stations)
                            {
                                isActuallyGray = true;
                                break;
                            }
                        }
                    }

                    if (ShowInfoviewBackground != isActuallyGray)
                    {
                        ShowInfoviewBackground = isActuallyGray;
                        this.showInfoviewBackgroundBinding.Update(isActuallyGray);
                        BetterTransitView.ModSettings.ModSettings.Instance.MapModeActivatedByDefault = isActuallyGray;
                        BetterTransitView.ModSettings.ModSettings.Instance.Apply();
                    }
                }
                
                m_TransitUpdateFrame++;
                // Update data every 60 frames OR instantly if the dirty flag was tripped by a click
                if (m_TransitUpdateFrame % 60 == 0 || m_TransitLinesDirty) 
                {
                    UpdateTransitLinesData();
                    m_TransitLinesDirty = false;
                }
            }
            
            // Aggressively override vanilla infoview hijacking when a tool is selected
            if (m_EnforceInfoviewFrames > 0)
            {
                if (ShowInfoviewBackground && m_CustomInfoviewEntity != Entity.Null)
                {
                    m_InfoviewsUISystem.SetActiveInfoview(m_CustomInfoviewEntity);
                }
                else
                {
                    m_InfoviewsUISystem.SetActiveInfoview(Entity.Null);
                }
                m_EnforceInfoviewFrames--;
            }

            base.OnUpdate();
        }

        private int FindClosestRouteTo(Unity.Mathematics.float3 hitPos)
        {
            float minDistanceSq = float.MaxValue;
            int closestRouteIndex = 0;

            var segmentToRouteMap = new Unity.Collections.NativeParallelMultiHashMap<Entity, Entity>(200000, Unity.Collections.Allocator.Temp);
            using var routeEntities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            var segmentBufferLookup = GetBufferLookup<Game.Routes.RouteSegment>(true);
            var pathElementLookup = GetBufferLookup<Game.Pathfind.PathElement>(true);
            var curveLookup = GetComponentLookup<Game.Net.Curve>(true);

            // Build map
            foreach (var routeEntity in routeEntities)
            {
                if (HiddenCustomRoutes.Contains(routeEntity)) continue;
                if (segmentBufferLookup.TryGetBuffer(routeEntity, out var segments))
                {
                    foreach (var segment in segments)
                    {
                        if (pathElementLookup.TryGetBuffer(segment.m_Segment, out var path))
                        {
                            foreach (var element in path) segmentToRouteMap.Add(element.m_Target, routeEntity);
                        }
                    }
                }
            }

            var cameraUpdateSystem = World.GetExistingSystemManaged<Game.Rendering.CameraUpdateSystem>();
            float zoomLevel = cameraUpdateSystem != null ? cameraUpdateSystem.zoom : 5000f;
            float normalizedZoom = Unity.Mathematics.math.clamp((zoomLevel - 1600f) / (10000f - 1600f), 0f, 1f);
            float thickness = Unity.Mathematics.math.lerp(4.0f, 4.0f * 12f, normalizedZoom);
            float ribbonWidth = thickness * 0.85f;

            foreach (var routeEntity in routeEntities)
            {
                if (HiddenCustomRoutes.Contains(routeEntity)) continue;
                if (segmentBufferLookup.TryGetBuffer(routeEntity, out var segments))
                {
                    foreach (var segment in segments)
                    {
                        if (pathElementLookup.TryGetBuffer(segment.m_Segment, out var path))
                        {
                            foreach (var element in path)
                            {
                                if (curveLookup.TryGetComponent(element.m_Target, out var curveComponent))
                                {
                                    var myCurve = curveComponent.m_Bezier;
                                    var uniqueRoutes = new Unity.Collections.NativeList<Entity>(16, Unity.Collections.Allocator.Temp);
                                    if (segmentToRouteMap.TryGetFirstValue(element.m_Target, out Entity routeOnSegment, out var iterator))
                                    {
                                        do
                                        {
                                            if (!uniqueRoutes.Contains(routeOnSegment)) uniqueRoutes.Add(routeOnSegment);
                                        } while (segmentToRouteMap.TryGetNextValue(out routeOnSegment, ref iterator));
                                    }

                                    int totalLines = uniqueRoutes.Length;
                                    float scaleFactor = 1.0f;
                                    if (totalLines > 1) scaleFactor = Unity.Mathematics.math.max(0.35f, 1.0f - ((totalLines - 1) * 0.30f));
                                    float currentRibbonWidth = ribbonWidth * scaleFactor;

                                    if (totalLines > 1)
                                    {
                                        int myIndex = 0;
                                        for (int u = 0; u < totalLines; u++)
                                        {
                                            if (uniqueRoutes[u].Index < routeEntity.Index) myIndex++;
                                        }
                                        float offsetAmount = (myIndex - (totalLines - 1) / 2f) * currentRibbonWidth;
                                        var tangentA = Colossal.Mathematics.MathUtils.Tangent(myCurve, 0f);
                                        var tangentD = Colossal.Mathematics.MathUtils.Tangent(myCurve, 1f);
                                        var up = new Unity.Mathematics.float3(0, 1, 0);
                                        var rightA = Unity.Mathematics.math.normalizesafe(Unity.Mathematics.math.cross(up, tangentA));
                                        var rightD = Unity.Mathematics.math.normalizesafe(Unity.Mathematics.math.cross(up, tangentD));
                                        var rightMid = Unity.Mathematics.math.normalizesafe(rightA + rightD);

                                        myCurve.a += rightA * offsetAmount;
                                        myCurve.b += rightMid * offsetAmount;
                                        myCurve.c += rightMid * offsetAmount;
                                        myCurve.d += rightD * offsetAmount;
                                    }
                                    uniqueRoutes.Dispose();

                                    // Check distance
                                    for (float t = 0f; t <= 1.0f; t += 0.1f)
                                    {
                                        var pt = Colossal.Mathematics.MathUtils.Position(myCurve, t);
                                        // Ignore Y axis so hit detects correctly even if terrain is lower
                                        float distSq = Unity.Mathematics.math.distancesq(new Unity.Mathematics.float2(hitPos.x, hitPos.z), new Unity.Mathematics.float2(pt.x, pt.z));
                                        if (distSq < minDistanceSq)
                                        {
                                            minDistanceSq = distSq;
                                            closestRouteIndex = routeEntity.Index;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            segmentToRouteMap.Dispose();
            
            // Threshold: Ribbon max thickness is ~48, so we check distance up to ~50 meters
            if (minDistanceSq <= 2500f)
            {
                return closestRouteIndex;
            }
            return 0;
        }

        
        private struct RouteHeader
        {
            public int id;
            public string name;
            public string color;
            public string type;
        }

        private struct CollectedStop
        {
            public Entity wp;
            public Entity stopTarget;
            public Entity stationBuilding;
            public Unity.Mathematics.float3 position;
            public string name;
            public int waiting;
            public int waitTime;
        }

        private class CollectedLine
        {
            public Entity entity;
            public string type;
            public string name;
            public string colorHex;
            public int vehicles;
            public bool isDispatching;
            public bool hasShortage;
            public int passengers;
            public int waitingPassengers;
            public int avgWaitTime;
            public string lengthStr;
            public float lengthRaw;
            public int usage;
            public bool isCargo;
            public bool isVisible;
            public List<CollectedStop> stops;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 10);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 32)
                        {
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static float SanitizeFloat(float value, float fallback = 0f)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            return value;
        }

        private void UpdateTransitLinesData()
        {
            if (!this.IsTransitPanelActive) return;

            try
            {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                var nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();

                var waitingPassengersLookup = GetComponentLookup<Game.Routes.WaitingPassengers>(true);
                var connectedLookup = GetComponentLookup<Game.Routes.Connected>(true);
                var transportStopLookup = GetComponentLookup<Game.Routes.TransportStop>(true);
                var transformLookup = GetComponentLookup<Game.Objects.Transform>(true);

                float rawTimeFactor = BetterTransitView.Utils.Time2WorkInterop.GetTimeFactor();
                float timeFactor = SanitizeFloat(rawTimeFactor, 1f);
                if (timeFactor < 1f) timeFactor = 1f;

                var collectedLines = new List<CollectedLine>(entities.Length);
                var targetToRoutes = new Dictionary<Entity, List<RouteHeader>>();
                var stationToRoutes = new Dictionary<(Entity, string), List<RouteHeader>>();
                var spatialGrid = new Dictionary<long, List<(Unity.Mathematics.float3 pos, RouteHeader route)>>();

                // ==========================================
                // PASS 1: Extract and Index all Lines & Stops
                // ==========================================
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    try
                    {
                        if (!EntityManager.TryGetComponent<Game.Routes.Color>(entity, out var colorComp)) continue;
                        if (!EntityManager.TryGetComponent<Game.Prefabs.PrefabRef>(entity, out var prefabRef)) continue;

                        var prefab = prefabSystem.GetPrefab<Game.Prefabs.TransportLinePrefab>(prefabRef.m_Prefab);
                        if (prefab == null) continue;

                        string type = prefab.m_TransportType.ToString().ToLower();
                        if (type != "bus" && type != "train" && type != "tram" && type != "subway" && type != "ferry" && type != "ship" && type != "airplane")
                        {
                            HiddenCustomRoutes.Add(entity);
                            continue;
                        }

                        bool isVisible = !HiddenCustomRoutes.Contains(entity);

                        string displayType = "Route";
                        if (!string.IsNullOrEmpty(type) && type != "none") displayType = char.ToUpper(type[0]) + type.Substring(1);

                        string name;
                        if (nameSystem.TryGetCustomName(entity, out string customName) && !string.IsNullOrEmpty(customName))
                        {
                            name = customName;
                        }
                        else if (EntityManager.TryGetComponent<Game.Routes.RouteNumber>(entity, out var routeNum))
                        {
                            int num = routeNum.m_Number;
                            name = num == 0 ? $"{displayType} Line {entity.Index}" : $"{displayType} Line {num}";
                        }
                        else
                        {
                            name = $"{displayType} Line {entity.Index}";
                        }

                        string colorHex = string.Format("#{0:X2}{1:X2}{2:X2}", colorComp.m_Color.r, colorComp.m_Color.g, colorComp.m_Color.b);

                        var routeHeader = new RouteHeader
                        {
                            id = entity.Index,
                            name = name,
                            color = colorHex,
                            type = type
                        };

                        int cargo = 0;
                        int capacity = 0;
                        bool isDispatching = false;
                        bool hasShortage = false;

                        if (EntityManager.TryGetComponent<Game.Routes.TransportLine>(entity, out var transportLine))
                        {
                            isDispatching = (transportLine.m_Flags & Game.Routes.TransportLineFlags.RequireVehicles) != 0;
                            hasShortage = (transportLine.m_Flags & Game.Routes.TransportLineFlags.NotEnoughVehicles) != 0;
                        }

                        int vehicles = TransportUIUtils.GetRouteVehiclesCount(EntityManager, entity, ref cargo, ref capacity);
                        int usage = capacity > 0 ? UnityEngine.Mathf.RoundToInt(((float)cargo / capacity) * 100) : 0;
                        usage = UnityEngine.Mathf.Clamp(usage, 0, 200);

                        float length = SanitizeFloat(TransportUIUtils.GetRouteLength(EntityManager, entity), 0f);
                        bool isImperial = Game.Settings.SharedSettings.instance != null &&
                                          Game.Settings.SharedSettings.instance.userInterface != null &&
                                          Game.Settings.SharedSettings.instance.userInterface.unitSystem == Game.Settings.InterfaceSettings.UnitSystem.Freedom;
                        string lengthStr = isImperial
                            ? (length / 1609.344f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " mi"
                            : (length / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km";

                        int totalWaitingPassengers = 0;
                        float totalWaitTimeSum = 0f;
                        int stopsWithWaitCount = 0;
                        int stopIndexCounter = 1;
                        var stopList = new List<CollectedStop>();

                        if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<Game.Routes.RouteWaypoint> waypoints))
                        {
                            for (int w = 0; w < waypoints.Length; w++)
                            {
                                try
                                {
                                    Entity wp = waypoints[w].m_Waypoint;
                                    bool isStop = transportStopLookup.HasComponent(wp) || connectedLookup.HasComponent(wp);
                                    if (!isStop) continue;

                                    int stopWaiting = 0;
                                    float rawWaitTime = 0f;

                                    if (waitingPassengersLookup.TryGetComponent(wp, out var wpPassengers))
                                    {
                                        stopWaiting += wpPassengers.m_Count;
                                        if (wpPassengers.m_Count > 0)
                                        {
                                            rawWaitTime = SanitizeFloat(wpPassengers.m_AverageWaitingTime, 0f);
                                        }
                                    }

                                    if (connectedLookup.TryGetComponent(wp, out var connected))
                                    {
                                        Entity stopBuilding = connected.m_Connected;
                                        if (stopBuilding != Entity.Null && waitingPassengersLookup.TryGetComponent(stopBuilding, out var bPassengers))
                                        {
                                            stopWaiting += bPassengers.m_Count;
                                            if (bPassengers.m_Count > 0 && rawWaitTime == 0f)
                                            {
                                                rawWaitTime = SanitizeFloat(bPassengers.m_AverageWaitingTime, 0f);
                                            }
                                        }
                                    }

                                    rawWaitTime = SanitizeFloat(rawWaitTime, 0f);
                                    int scaledWaitMin = rawWaitTime > 0 ? (int)System.Math.Round(rawWaitTime / timeFactor) : 0;
                                    scaledWaitMin = System.Math.Max(0, scaledWaitMin);

                                    totalWaitingPassengers += stopWaiting;
                                    if (stopWaiting > 0)
                                    {
                                        totalWaitTimeSum += scaledWaitMin;
                                        stopsWithWaitCount++;
                                    }

                                    Entity stopTarget = connectedLookup.TryGetComponent(wp, out var connectedComp) ? connectedComp.m_Connected : wp;
                                    Entity stopStationBuilding = GetStationBuilding(wp, stopTarget);
                                    string stopName = GetStopResolvedName(wp, stopTarget, nameSystem, stopIndexCounter);
                                    stopIndexCounter++;

                                    Unity.Mathematics.float3 stopPos = Unity.Mathematics.float3.zero;
                                    if (transformLookup.TryGetComponent(wp, out var wpTrans)) stopPos = wpTrans.m_Position;
                                    else if (stopTarget != Entity.Null && transformLookup.TryGetComponent(stopTarget, out var targetTrans)) stopPos = targetTrans.m_Position;

                                    var collectedStop = new CollectedStop
                                    {
                                        wp = wp,
                                        stopTarget = stopTarget,
                                        stationBuilding = stopStationBuilding,
                                        position = stopPos,
                                        name = stopName,
                                        waiting = stopWaiting,
                                        waitTime = scaledWaitMin
                                    };
                                    stopList.Add(collectedStop);

                                    // Index direct waypoint connections
                                    if (!targetToRoutes.TryGetValue(wp, out var wpRoutesList))
                                    {
                                        wpRoutesList = new List<RouteHeader>();
                                        targetToRoutes[wp] = wpRoutesList;
                                    }
                                    wpRoutesList.Add(routeHeader);

                                    // Index connected target (platform / shelter / stop target)
                                    if (stopTarget != Entity.Null && stopTarget != wp)
                                    {
                                        if (!targetToRoutes.TryGetValue(stopTarget, out var targetRoutesList))
                                        {
                                            targetRoutesList = new List<RouteHeader>();
                                            targetToRoutes[stopTarget] = targetRoutesList;
                                        }
                                        targetRoutesList.Add(routeHeader);
                                    }

                                    // Index station building of same transport type
                                    if (stopStationBuilding != Entity.Null)
                                    {
                                        var stationKey = (stopStationBuilding, type);
                                        if (!stationToRoutes.TryGetValue(stationKey, out var stationRoutesList))
                                        {
                                            stationRoutesList = new List<RouteHeader>();
                                            stationToRoutes[stationKey] = stationRoutesList;
                                        }
                                        stationRoutesList.Add(routeHeader);
                                    }

                                    // Index spatial grid
                                    if (!stopPos.Equals(Unity.Mathematics.float3.zero))
                                    {
                                        int cellX = (int)System.Math.Floor(stopPos.x / 128f);
                                        int cellZ = (int)System.Math.Floor(stopPos.z / 128f);
                                        long cellKey = ((long)cellX << 32) | (uint)cellZ;

                                        if (!spatialGrid.TryGetValue(cellKey, out var gridBucket))
                                        {
                                            gridBucket = new List<(Unity.Mathematics.float3 pos, RouteHeader route)>();
                                            spatialGrid[cellKey] = gridBucket;
                                        }
                                        gridBucket.Add((stopPos, routeHeader));
                                    }
                                }
                                catch (System.Exception stopEx)
                                {
                                    Mod.log.Warn(stopEx, $"Error processing waypoint {w} for transit line {entity.Index}");
                                }
                            }
                        }

                        int avgWaitTime = stopsWithWaitCount > 0 ? (int)System.Math.Round(totalWaitTimeSum / stopsWithWaitCount) : 0;
                        avgWaitTime = System.Math.Max(0, avgWaitTime);

                        bool isCargo = false;
                        if (EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(prefabRef.m_Prefab, out var lineData))
                        {
                            isCargo = lineData.m_CargoTransport;
                        }

                        collectedLines.Add(new CollectedLine
                        {
                            entity = entity,
                            type = type,
                            name = name,
                            colorHex = colorHex,
                            vehicles = vehicles,
                            isDispatching = isDispatching,
                            hasShortage = hasShortage,
                            passengers = cargo,
                            waitingPassengers = totalWaitingPassengers,
                            avgWaitTime = avgWaitTime,
                            lengthStr = lengthStr,
                            lengthRaw = length,
                            usage = usage,
                            isCargo = isCargo,
                            isVisible = isVisible,
                            stops = stopList
                        });
                    }
                    catch (System.Exception lineEx)
                    {
                        Mod.log.Warn(lineEx, $"Error extracting data for transit line entity {entity.Index}");
                    }
                }

                // ==========================================
                // PASS 2: Assemble High-Performance JSON
                // ==========================================
                var result = new System.Text.StringBuilder(collectedLines.Count * 256 + 16);
                result.Append("[");
                bool first = true;

                for (int i = 0; i < collectedLines.Count; i++)
                {
                    var line = collectedLines[i];
                    try
                    {
                        var stopsJson = new System.Text.StringBuilder(line.stops.Count * 128 + 16);
                        stopsJson.Append("[");
                        bool firstStop = true;

                        for (int s = 0; s < line.stops.Count; s++)
                        {
                            var stop = line.stops[s];

                            // Direct Connected Lines
                            var connectedRoutes = new Dictionary<int, RouteHeader>();
                            if (targetToRoutes.TryGetValue(stop.wp, out var wpRoutes))
                            {
                                for (int r = 0; r < wpRoutes.Count; r++)
                                {
                                    if (wpRoutes[r].id != line.entity.Index) connectedRoutes[wpRoutes[r].id] = wpRoutes[r];
                                }
                            }
                            if (stop.stopTarget != Entity.Null && stop.stopTarget != stop.wp && targetToRoutes.TryGetValue(stop.stopTarget, out var targetRoutes))
                            {
                                for (int r = 0; r < targetRoutes.Count; r++)
                                {
                                    if (targetRoutes[r].id != line.entity.Index) connectedRoutes[targetRoutes[r].id] = targetRoutes[r];
                                }
                            }
                            if (stop.stationBuilding != Entity.Null && stationToRoutes.TryGetValue((stop.stationBuilding, line.type), out var stationRoutes))
                            {
                                for (int r = 0; r < stationRoutes.Count; r++)
                                {
                                    if (stationRoutes[r].id != line.entity.Index) connectedRoutes[stationRoutes[r].id] = stationRoutes[r];
                                }
                            }

                            var connLinesJson = new System.Text.StringBuilder("[");
                            bool firstConn = true;
                            foreach (var kvp in connectedRoutes)
                            {
                                var r = kvp.Value;
                                if (!firstConn) connLinesJson.Append(",");
                                connLinesJson.Append($"{{\"id\": {r.id}, \"name\": \"{EscapeJson(r.name)}\", \"color\": \"{EscapeJson(r.color)}\", \"type\": \"{EscapeJson(r.type)}\"}}");
                                firstConn = false;
                            }
                            connLinesJson.Append("]");

                            // Nearby Lines (within 120m)
                            var nearbyRoutes = new Dictionary<int, RouteHeader>();
                            if (!stop.position.Equals(Unity.Mathematics.float3.zero))
                            {
                                int cellX = (int)System.Math.Floor(stop.position.x / 128f);
                                int cellZ = (int)System.Math.Floor(stop.position.z / 128f);

                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    for (int dz = -1; dz <= 1; dz++)
                                    {
                                        long neighborKey = ((long)(cellX + dx) << 32) | (uint)(cellZ + dz);
                                        if (spatialGrid.TryGetValue(neighborKey, out var candidateStops))
                                        {
                                            for (int c = 0; c < candidateStops.Count; c++)
                                            {
                                                var cand = candidateStops[c];
                                                if (cand.route.id == line.entity.Index) continue;
                                                if (connectedRoutes.ContainsKey(cand.route.id)) continue;
                                                if (nearbyRoutes.ContainsKey(cand.route.id)) continue;

                                                float distSq = Unity.Mathematics.math.distancesq(
                                                    new Unity.Mathematics.float2(stop.position.x, stop.position.z),
                                                    new Unity.Mathematics.float2(cand.pos.x, cand.pos.z)
                                                );
                                                if (distSq <= 14400.0f) // 120m
                                                {
                                                    nearbyRoutes[cand.route.id] = cand.route;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            var nearbyLinesJson = new System.Text.StringBuilder("[");
                            bool firstNearby = true;
                            foreach (var kvp in nearbyRoutes)
                            {
                                var r = kvp.Value;
                                if (!firstNearby) nearbyLinesJson.Append(",");
                                nearbyLinesJson.Append($"{{\"id\": {r.id}, \"name\": \"{EscapeJson(r.name)}\", \"color\": \"{EscapeJson(r.color)}\", \"type\": \"{EscapeJson(r.type)}\"}}");
                                firstNearby = false;
                            }
                            nearbyLinesJson.Append("]");

                            if (!firstStop) stopsJson.Append(",");
                            stopsJson.Append($"{{\"id\": {stop.wp.Index}, \"targetId\": {stop.stopTarget.Index}, \"name\": \"{EscapeJson(stop.name)}\", \"waiting\": {stop.waiting}, \"waitTime\": {stop.waitTime}, \"connectingLines\": {connLinesJson.ToString()}, \"nearbyLines\": {nearbyLinesJson.ToString()}}}");
                            firstStop = false;
                        }
                        stopsJson.Append("]");

                        if (!first) result.Append(",");
                        result.Append($"{{\"id\": {line.entity.Index}, \"type\": \"{EscapeJson(line.type)}\", \"name\": \"{EscapeJson(line.name)}\", \"color\": \"{EscapeJson(line.colorHex)}\", \"vehicles\": {line.vehicles}, \"isDispatching\": {line.isDispatching.ToString().ToLower()}, \"hasShortage\": {line.hasShortage.ToString().ToLower()}, \"passengers\": {line.passengers}, \"waitingPassengers\": {line.waitingPassengers}, \"avgWaitTime\": {line.avgWaitTime}, \"length\": \"{EscapeJson(line.lengthStr)}\", \"lengthRaw\": {line.lengthRaw.ToString(System.Globalization.CultureInfo.InvariantCulture)}, \"usage\": {line.usage}, \"cargo\": {line.isCargo.ToString().ToLower()}, \"visible\": {line.isVisible.ToString().ToLower()}, \"stops\": {line.stops.Count}, \"stopList\": {stopsJson.ToString()} }}");
                        first = false;
                    }
                    catch (System.Exception lineJsonEx)
                    {
                        Mod.log.Warn(lineJsonEx, $"Error serializing transit line {line.entity.Index} to JSON");
                    }
                }

                result.Append("]");
                this.transitLinesDataBinding.Update(result.ToString());
            }
            catch (System.Exception ex)
            {
                Mod.log.Error(ex, "Error updating transit lines data in TransitUISystem");
            }
        }

        private void ActivateTransitMode(string mode)
        {
            m_ActiveTransitMode = mode;
            this.showTransitPanelBinding.Update(true);
            
            HiddenCustomRoutes.Clear();

            if (m_HasInitializedDefaults)
            {
                foreach (var e in m_SavedHiddenRoutes) HiddenCustomRoutes.Add(e);
            }
            else
            {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach(var e in entities) {
                    if (!EntityManager.HasComponent<Game.Prefabs.PrefabRef>(e)) continue;
                    var prefabRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(e);
                    if (EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(prefabRef.m_Prefab, out var lineData)) {
                        if (lineData.m_CargoTransport) {
                            if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultCargoVisible) {
                                HiddenCustomRoutes.Add(e);
                            }
                        } else {
                            switch (lineData.m_TransportType) {
                                case Game.Prefabs.TransportType.Bus:
                                    if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultBusVisible) HiddenCustomRoutes.Add(e);
                                    break;
                                case Game.Prefabs.TransportType.Train:
                                    if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultTrainVisible) HiddenCustomRoutes.Add(e);
                                    break;
                                case Game.Prefabs.TransportType.Tram:
                                    if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultTramVisible) HiddenCustomRoutes.Add(e);
                                    break;
                                case Game.Prefabs.TransportType.Subway:
                                    if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultSubwayVisible) HiddenCustomRoutes.Add(e);
                                    break;
                                case Game.Prefabs.TransportType.Ship:
                                case Game.Prefabs.TransportType.Ferry:
                                    if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultShipVisible) HiddenCustomRoutes.Add(e);
                                    break;
                                case Game.Prefabs.TransportType.Airplane:
                                    if (!BetterTransitView.ModSettings.ModSettings.Instance.DefaultAirplaneVisible) HiddenCustomRoutes.Add(e);
                                    break;
                            }
                        }
                    }
                }
                m_HasInitializedDefaults = true;
            }

            UpdateTransitLinesData(); 

            if (mode == "custom" && m_CustomInfoviewEntity != Entity.Null)
            {
                if (ShowInfoviewBackground) m_InfoviewsUISystem.SetActiveInfoview(m_CustomInfoviewEntity);
            }
            
            SyncVanillaVisibilityToUI();
        }

        private void DeactivateTransitMode()
        {
            m_ActiveTransitMode = "none";
            this.showTransitPanelBinding.Update(false);
            m_InfoviewsUISystem.SetActiveInfoview(Entity.Null);
            
            m_SavedHiddenRoutes.Clear();
            foreach (var e in HiddenCustomRoutes) m_SavedHiddenRoutes.Add(e);
            
            HiddenCustomRoutes.Clear();
            SyncVanillaVisibilityToUI();
        }

        private void SetupCustomInfoview()
        {
            var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
            var infoviewInitSystem = World.GetOrCreateSystemManaged<Game.Prefabs.InfoviewInitializeSystem>();

            m_CustomInfoview = UnityEngine.ScriptableObject.CreateInstance<Game.Prefabs.InfoviewPrefab>();
            m_CustomInfoview.name = "BetterTransitViewTransitView";
            m_CustomInfoview.m_Group = -1; 
            m_CustomInfoview.m_Priority = 1;

            var stationMode = UnityEngine.ScriptableObject.CreateInstance<Game.Prefabs.BuildingStatusInfomodePrefab>();
            stationMode.name = "BetterTransitViewStations";
            stationMode.m_Type = (Game.Prefabs.BuildingStatusType)BetterTransitViewStatusType.Stations;
            stationMode.m_Low = new UnityEngine.Color(0.2f, 0.6f, 1.0f);
            stationMode.m_Medium = new UnityEngine.Color(0.2f, 0.6f, 1.0f); 
            stationMode.m_High = new UnityEngine.Color(0.2f, 0.6f, 1.0f); 

            prefabSystem.AddPrefab(stationMode);

            var combinedModes = new System.Collections.Generic.List<Game.Prefabs.InfomodeInfo>();

            // Copy terrain darkening
            if (infoviewInitSystem != null && infoviewInitSystem.infoviews != null)
            {
                foreach (var vanillaView in infoviewInitSystem.infoviews)
                {
                    if (vanillaView.name == "PublicTransport")
                    {
                        foreach (var modeInfo in vanillaView.m_Infomodes)
                        {
                            if (modeInfo.m_Mode is Game.Prefabs.BuildingStatusInfomodePrefab) continue;
                            combinedModes.Add(modeInfo);
                        }
                        break;
                    }
                }
            }

            combinedModes.Add(new Game.Prefabs.InfomodeInfo() { m_Mode = stationMode, m_Priority = 100 });

            m_CustomInfoview.m_Infomodes = combinedModes.ToArray();
            prefabSystem.AddPrefab(m_CustomInfoview);
            m_CustomInfoviewEntity = prefabSystem.GetEntity(m_CustomInfoview);
        }

        private void SyncVanillaVisibilityToUI()
        {
            if (m_TransitLinesQuery.IsEmptyIgnoreFilter) return;

            using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            bool needsUpdate = false;

            // Pass 1: Quick check
            foreach (var entity in entities)
            {
                bool isHiddenInUI = HiddenCustomRoutes.Contains(entity);
                bool hasVanillaHidden = EntityManager.HasComponent<Game.Routes.HiddenRoute>(entity);

                if (isHiddenInUI != hasVanillaHidden)
                {
                    needsUpdate = true;
                    break;
                }
            }

            if (!needsUpdate) return;

            // Pass 2: Safely apply changes
            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            foreach (var entity in entities)
            {
                bool isHiddenInUI = HiddenCustomRoutes.Contains(entity);
                bool hasVanillaHidden = EntityManager.HasComponent<Game.Routes.HiddenRoute>(entity);

                if (isHiddenInUI && !hasVanillaHidden)
                {
                    ecb.AddComponent<Game.Routes.HiddenRoute>(entity);
                }
                else if (!isHiddenInUI && hasVanillaHidden)
                {
                    ecb.RemoveComponent<Game.Routes.HiddenRoute>(entity);
                }
            }
        }

        private Entity GetStationBuilding(Entity wp, Entity stopTarget)
        {
            try
            {
                Entity check = stopTarget != Entity.Null ? stopTarget : wp;
                if (check == Entity.Null) return Entity.Null;

                if (EntityManager.HasComponent<Game.Buildings.Building>(check))
                    return check;

                var targets = new Entity[] { check, wp };
                foreach (var targetEnt in targets)
                {
                    if (targetEnt == Entity.Null) continue;

                    if (EntityManager.HasComponent<Game.Buildings.Building>(targetEnt))
                        return targetEnt;

                    // Check Owner Building chain
                    if (EntityManager.TryGetComponent<Game.Common.Owner>(targetEnt, out var ownerComp))
                    {
                        Entity owner = ownerComp.m_Owner;
                        int depth = 0;
                        while (owner != Entity.Null && depth < 5)
                        {
                            depth++;
                            if (EntityManager.HasComponent<Game.Buildings.Building>(owner))
                                return owner;
                            if (EntityManager.TryGetComponent<Game.Common.Owner>(owner, out var nextOwnerComp))
                                owner = nextOwnerComp.m_Owner;
                            else if (EntityManager.TryGetComponent<Game.Objects.Attached>(owner, out var attachedComp))
                                owner = attachedComp.m_Parent;
                            else
                                break;
                        }
                    }

                    // Check Attached Parent Building chain
                    if (EntityManager.TryGetComponent<Game.Objects.Attached>(targetEnt, out var topAttachedComp))
                    {
                        Entity parent = topAttachedComp.m_Parent;
                        int depth = 0;
                        while (parent != Entity.Null && depth < 5)
                        {
                            depth++;
                            if (EntityManager.HasComponent<Game.Buildings.Building>(parent))
                                return parent;
                            if (EntityManager.TryGetComponent<Game.Objects.Attached>(parent, out var nextAttachedComp))
                                parent = nextAttachedComp.m_Parent;
                            else if (EntityManager.TryGetComponent<Game.Common.Owner>(parent, out var nextOwnerComp))
                                parent = nextOwnerComp.m_Owner;
                            else
                                break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Mod.log.Warn(ex, "Error resolving station building for stop");
            }

            return Entity.Null;
        }

        private string GetStopResolvedName(Entity wp, Entity stopTarget, Game.UI.NameSystem nameSystem, int stopIndexCounter)
        {
            string baseName = null;
            try
            {
                Entity displayEnt = stopTarget != Entity.Null ? stopTarget : wp;
                var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();

                // 1. Try Custom Name on Waypoint
                if (nameSystem.TryGetCustomName(wp, out string name1) && !string.IsNullOrEmpty(name1))
                {
                    baseName = name1;
                }
                // 2. Try Custom Name on Connected Stop Target (Platform / Building / Shelter)
                else if (stopTarget != Entity.Null && stopTarget != wp && nameSystem.TryGetCustomName(stopTarget, out string name2) && !string.IsNullOrEmpty(name2))
                {
                    baseName = name2;
                }
                else
                {
                    var targets = new Entity[] { stopTarget, wp };

                    // 3. Try Owner or Parent Attached Building Entity (excluding Route transit lines & roads)
                    foreach (var targetEnt in targets)
                    {
                        if (targetEnt == Entity.Null) continue;

                        // Check Owner Building
                        if (EntityManager.TryGetComponent<Game.Common.Owner>(targetEnt, out var ownerComp))
                        {
                            Entity owner = ownerComp.m_Owner;
                            if (owner != Entity.Null && !EntityManager.HasComponent<Game.Routes.Route>(owner))
                            {
                                if (nameSystem.TryGetCustomName(owner, out string ownerCustom) && !string.IsNullOrEmpty(ownerCustom))
                                {
                                    baseName = ownerCustom;
                                    break;
                                }

                                if (EntityManager.HasComponent<Game.Buildings.Building>(owner) && EntityManager.TryGetComponent<Game.Prefabs.PrefabRef>(owner, out var pRef))
                                {
                                    var ownerPrefab = prefabSystem.GetPrefab<Game.Prefabs.PrefabBase>(pRef);
                                    if (ownerPrefab != null && !string.IsNullOrEmpty(ownerPrefab.name))
                                    {
                                        string locKey = $"Assets.NAME[{ownerPrefab.name}]";
                                        if (Game.SceneFlow.GameManager.instance?.localizationManager?.activeDictionary != null &&
                                            Game.SceneFlow.GameManager.instance.localizationManager.activeDictionary.TryGetValue(locKey, out string locTitle) &&
                                            !string.IsNullOrEmpty(locTitle))
                                        {
                                            baseName = locTitle;
                                            break;
                                        }

                                        string pName = CleanStopPrefabName(ownerPrefab.name);
                                        if (!string.IsNullOrEmpty(pName))
                                        {
                                            baseName = pName;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // Check Attached Parent Building
                        if (EntityManager.TryGetComponent<Game.Objects.Attached>(targetEnt, out var attachedComp))
                        {
                            Entity parent = attachedComp.m_Parent;
                            if (parent != Entity.Null && !EntityManager.HasComponent<Game.Routes.Route>(parent))
                            {
                                if (nameSystem.TryGetCustomName(parent, out string parentCustom) && !string.IsNullOrEmpty(parentCustom))
                                {
                                    baseName = parentCustom;
                                    break;
                                }

                                if (EntityManager.HasComponent<Game.Buildings.Building>(parent) && EntityManager.TryGetComponent<Game.Prefabs.PrefabRef>(parent, out var pRef))
                                {
                                    var parentPrefab = prefabSystem.GetPrefab<Game.Prefabs.PrefabBase>(pRef);
                                    if (parentPrefab != null && !string.IsNullOrEmpty(parentPrefab.name))
                                    {
                                        string locKey = $"Assets.NAME[{parentPrefab.name}]";
                                        if (Game.SceneFlow.GameManager.instance?.localizationManager?.activeDictionary != null &&
                                            Game.SceneFlow.GameManager.instance.localizationManager.activeDictionary.TryGetValue(locKey, out string locTitle) &&
                                            !string.IsNullOrEmpty(locTitle))
                                        {
                                            baseName = locTitle;
                                            break;
                                        }

                                        string pName = CleanStopPrefabName(parentPrefab.name);
                                        if (!string.IsNullOrEmpty(pName))
                                        {
                                            baseName = pName;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 4. Try PrefabRef directly on Station Buildings / Transport Stops ONLY
                    if (string.IsNullOrEmpty(baseName))
                    {
                        foreach (var targetEnt in targets)
                        {
                            if (targetEnt == Entity.Null) continue;

                            bool isBuilding = EntityManager.HasComponent<Game.Buildings.Building>(targetEnt);
                            bool isTransportStop = EntityManager.HasComponent<Game.Routes.TransportStop>(targetEnt);
                            bool isNetSegment = EntityManager.HasComponent<Game.Net.Edge>(targetEnt) || EntityManager.HasComponent<Game.Net.Node>(targetEnt);

                            if ((isBuilding || isTransportStop) && !isNetSegment && EntityManager.TryGetComponent<Game.Prefabs.PrefabRef>(targetEnt, out var pRef))
                            {
                                var stopPrefab = prefabSystem.GetPrefab<Game.Prefabs.PrefabBase>(pRef);
                                if (stopPrefab != null && !string.IsNullOrEmpty(stopPrefab.name))
                                {
                                    string locKey = $"Assets.NAME[{stopPrefab.name}]";
                                    if (Game.SceneFlow.GameManager.instance?.localizationManager?.activeDictionary != null &&
                                        Game.SceneFlow.GameManager.instance.localizationManager.activeDictionary.TryGetValue(locKey, out string locTitle) &&
                                        !string.IsNullOrEmpty(locTitle))
                                    {
                                        baseName = locTitle;
                                        break;
                                    }

                                    string pName = CleanStopPrefabName(stopPrefab.name);
                                    if (!string.IsNullOrEmpty(pName))
                                    {
                                        baseName = pName;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Mod.log.Warn(ex, "Error resolving stop name");
            }

            // 5. Fallback for Roadside Stops (e.g. "Bus Stop #1")
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = $"{GetStopPrefix(wp)} #{stopIndexCounter}";
            }

            return baseName;
        }

        private string GetStopPrefix(Entity stopEntity)
        {
            if (EntityManager.HasComponent<Game.Routes.BusStop>(stopEntity)) return "Bus Stop";
            if (EntityManager.HasComponent<Game.Routes.SubwayStop>(stopEntity)) return "Subway";
            if (EntityManager.HasComponent<Game.Routes.TrainStop>(stopEntity)) return "Platform";
            if (EntityManager.HasComponent<Game.Routes.TramStop>(stopEntity)) return "Tram";
            if (EntityManager.HasComponent<Game.Routes.ShipStop>(stopEntity)) return "Ship";
            if (EntityManager.HasComponent<Game.Routes.FerryStop>(stopEntity)) return "Ferry";
            if (EntityManager.HasComponent<Game.Routes.AirplaneStop>(stopEntity)) return "Gate";
            if (EntityManager.HasComponent<Game.Routes.TaxiStand>(stopEntity)) return "Taxi";
            return "Stop";
        }

        private string CleanStopPrefabName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "";

            // Ignore road / net prefabs so stop names are never "Small Road", "Highway", etc.
            if (rawName.IndexOf("Road", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawName.IndexOf("Highway", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawName.IndexOf("Street", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawName.IndexOf("Track", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawName.IndexOf("Path", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "";
            }

            string name = rawName.Replace('_', ' ');
            while (name.Length > 0 && char.IsDigit(name[name.Length - 1]))
            {
                name = name.Substring(0, name.Length - 1);
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]) && name[i - 1] != ' ')
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }
            return sb.ToString().Trim();
        }
    }
}