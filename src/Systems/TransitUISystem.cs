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

        
        private void UpdateTransitLinesData()
        {
            if (!this.IsTransitPanelActive) return;

            using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var result = new System.Text.StringBuilder("[");
            var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
            var nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            bool first = true;

            var waitingPassengersLookup = GetComponentLookup<Game.Routes.WaitingPassengers>(true);
            var connectedLookup = GetComponentLookup<Game.Routes.Connected>(true);
            var transportStopLookup = GetComponentLookup<Game.Routes.TransportStop>(true);
            var transformLookup = GetComponentLookup<Game.Objects.Transform>(true);
            float timeFactor = System.Math.Max(1f, BetterTransitView.Utils.Time2WorkInterop.GetTimeFactor());

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!EntityManager.HasComponent<Game.Routes.Color>(entity)) continue;
                
                var prefabRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(entity);
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
                if (nameSystem.TryGetCustomName(entity, out string customName))
                {
                    name = customName;
                }
                else
                {
                    if (EntityManager.TryGetComponent<Game.Routes.RouteNumber>(entity, out var routeNum))
                    {
                        int num = routeNum.m_Number;
                        name = num == 0 ? $"{displayType} Line {entity.Index}" : $"{displayType} Line {num}";
                    }
                    else 
                    {
                        name = "Unnamed Route";
                    }
                }

                var colorComp = EntityManager.GetComponentData<Game.Routes.Color>(entity); 
                string colorHex = string.Format("#{0:X2}{1:X2}{2:X2}", colorComp.m_Color.r, colorComp.m_Color.g, colorComp.m_Color.b);
                
                int cargo = 0;
                int capacity = 0;
                
                var transportLine = EntityManager.GetComponentData<Game.Routes.TransportLine>(entity);
                bool isDispatching = (transportLine.m_Flags & Game.Routes.TransportLineFlags.RequireVehicles) != 0;
                bool hasShortage = (transportLine.m_Flags & Game.Routes.TransportLineFlags.NotEnoughVehicles) != 0;
                int vehicles = TransportUIUtils.GetRouteVehiclesCount(EntityManager, entity, ref cargo, ref capacity);
                
                int usage = capacity > 0 ? UnityEngine.Mathf.RoundToInt(((float)cargo / capacity) * 100) : 0;
                
                float length = TransportUIUtils.GetRouteLength(EntityManager, entity);
                bool isImperial = Game.Settings.SharedSettings.instance.userInterface.unitSystem == Game.Settings.InterfaceSettings.UnitSystem.Freedom;
                string lengthStr = isImperial 
                    ? (length / 1609.344f).ToString("0.0") + " mi" 
                    : (length / 1000f).ToString("0.0") + " km";
                
                int stops = 0;
                int totalWaitingPassengers = 0;
                float totalWaitTimeSum = 0f;
                int stopsWithWaitCount = 0;
                var stopsJson = new System.Text.StringBuilder("[");
                bool firstStop = true;
                int stopIndexCounter = 1;

                if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<Game.Routes.RouteWaypoint> waypoints))
                {
                    for (int w = 0; w < waypoints.Length; w++)
                    {
                        Entity wp = waypoints[w].m_Waypoint;
                        
                        bool isStop = transportStopLookup.HasComponent(wp) || connectedLookup.HasComponent(wp);
                        if (!isStop) continue;

                        stops++;
                        int stopWaiting = 0;
                        float rawWaitTime = 0f;

                        if (waitingPassengersLookup.TryGetComponent(wp, out var wpPassengers))
                        {
                            stopWaiting += wpPassengers.m_Count;
                            if (wpPassengers.m_Count > 0)
                            {
                                rawWaitTime = wpPassengers.m_AverageWaitingTime;
                            }
                        }

                        if (connectedLookup.TryGetComponent(wp, out var connected))
                        {
                            Entity stopBuilding = connected.m_Connected;
                            if (waitingPassengersLookup.TryGetComponent(stopBuilding, out var bPassengers))
                            {
                                stopWaiting += bPassengers.m_Count;
                                if (bPassengers.m_Count > 0 && rawWaitTime == 0f)
                                {
                                    rawWaitTime = bPassengers.m_AverageWaitingTime;
                                }
                            }
                        }

                        int scaledWaitMin = rawWaitTime > 0 ? (int)System.Math.Round(rawWaitTime / timeFactor) : 0;

                        totalWaitingPassengers += stopWaiting;
                        if (stopWaiting > 0)
                        {
                            totalWaitTimeSum += scaledWaitMin;
                            stopsWithWaitCount++;
                        }

                        Entity stopTarget = connectedLookup.TryGetComponent(wp, out var connectedComp) ? connectedComp.m_Connected : wp;
                        Entity stopStationBuilding = GetStationBuilding(wp, stopTarget);

                        // Stop Name Resolution
                        string stopName = GetStopResolvedName(wp, stopTarget, nameSystem, stopIndexCounter);

                        stopIndexCounter++;
                        string safeStopName = stopName?.Replace("\"", "\\\"") ?? "Stop";

                        // Collect Connecting Lines at this stop
                        var connLinesJson = new System.Text.StringBuilder("[");
                        bool firstConn = true;
                        var directConnectedEntities = new System.Collections.Generic.HashSet<Entity>();

                        for (int o = 0; o < entities.Length; o++)
                        {
                            Entity otherEntity = entities[o];
                            if (otherEntity == entity) continue;
                            if (!EntityManager.HasComponent<Game.Routes.Color>(otherEntity)) continue;

                            if (EntityManager.TryGetBuffer(otherEntity, true, out DynamicBuffer<Game.Routes.RouteWaypoint> oWaypoints))
                            {
                                var oPrefabRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(otherEntity);
                                var oPrefab = prefabSystem.GetPrefab<Game.Prefabs.TransportLinePrefab>(oPrefabRef.m_Prefab);
                                string oTypeStr = oPrefab != null ? oPrefab.m_TransportType.ToString().ToLower() : "bus";

                                bool connects = false;
                                for (int ow = 0; ow < oWaypoints.Length; ow++)
                                {
                                    Entity oWp = oWaypoints[ow].m_Waypoint;
                                    Entity oTarget = connectedLookup.TryGetComponent(oWp, out var oConn) ? oConn.m_Connected : oWp;
                                    if (oWp == wp || (stopTarget != Entity.Null && (oWp == stopTarget || oTarget == stopTarget)))
                                    {
                                        connects = true;
                                        break;
                                    }

                                    // Check if same type of transit and stops at the same station building (e.g. different platforms of the same subway station)
                                    if (stopStationBuilding != Entity.Null && type == oTypeStr)
                                    {
                                        Entity oStationBuilding = GetStationBuilding(oWp, oTarget);
                                        if (oStationBuilding != Entity.Null && oStationBuilding == stopStationBuilding)
                                        {
                                            connects = true;
                                            break;
                                        }
                                    }
                                }

                                if (connects)
                                {
                                    directConnectedEntities.Add(otherEntity);

                                    string oName;
                                    if (nameSystem.TryGetCustomName(otherEntity, out string oCustomName) && !string.IsNullOrEmpty(oCustomName))
                                    {
                                        oName = oCustomName;
                                    }
                                    else if (EntityManager.TryGetComponent<Game.Routes.RouteNumber>(otherEntity, out var oNum))
                                    {
                                        string oType = oPrefab != null ? oPrefab.m_TransportType.ToString() : "Route";
                                        oName = oNum.m_Number == 0 ? $"{oType} {otherEntity.Index}" : $"{oType} {oNum.m_Number}";
                                    }
                                    else
                                    {
                                        oName = "Route";
                                    }

                                    var oColorComp = EntityManager.GetComponentData<Game.Routes.Color>(otherEntity);
                                    string oColorHex = string.Format("#{0:X2}{1:X2}{2:X2}", oColorComp.m_Color.r, oColorComp.m_Color.g, oColorComp.m_Color.b);

                                    string safeOName = oName?.Replace("\"", "\\\"") ?? "Route";
                                    if (!firstConn) connLinesJson.Append(",");
                                    connLinesJson.Append($@"{{""id"": {otherEntity.Index}, ""name"": ""{safeOName}"", ""color"": ""{oColorHex}"", ""type"": ""{oTypeStr}""}}");
                                    firstConn = false;
                                }
                            }
                        }
                        connLinesJson.Append("]");

                        // Collect Nearby Lines (within ~120m)
                        var nearbyLinesJson = new System.Text.StringBuilder("[");
                        bool firstNearby = true;

                        Unity.Mathematics.float3 stopPos = Unity.Mathematics.float3.zero;
                        if (transformLookup.TryGetComponent(wp, out var wpTrans)) stopPos = wpTrans.m_Position;
                        else if (stopTarget != Entity.Null && transformLookup.TryGetComponent(stopTarget, out var targetTrans)) stopPos = targetTrans.m_Position;

                        if (!stopPos.Equals(Unity.Mathematics.float3.zero))
                        {
                            for (int o = 0; o < entities.Length; o++)
                            {
                                Entity otherEntity = entities[o];
                                if (otherEntity == entity) continue;
                                if (directConnectedEntities.Contains(otherEntity)) continue;
                                if (!EntityManager.HasComponent<Game.Routes.Color>(otherEntity)) continue;

                                if (EntityManager.TryGetBuffer(otherEntity, true, out DynamicBuffer<Game.Routes.RouteWaypoint> oWaypoints))
                                {
                                    bool isNearby = false;
                                    for (int ow = 0; ow < oWaypoints.Length; ow++)
                                    {
                                        Entity oWp = oWaypoints[ow].m_Waypoint;
                                        Entity oTarget = connectedLookup.TryGetComponent(oWp, out var oConn) ? oConn.m_Connected : oWp;
                                        
                                        Unity.Mathematics.float3 oPos = Unity.Mathematics.float3.zero;
                                        if (transformLookup.TryGetComponent(oWp, out var oWpTrans)) oPos = oWpTrans.m_Position;
                                        else if (oTarget != Entity.Null && transformLookup.TryGetComponent(oTarget, out var oTargetTrans)) oPos = oTargetTrans.m_Position;

                                        if (!oPos.Equals(Unity.Mathematics.float3.zero))
                                        {
                                            float distSq = Unity.Mathematics.math.distancesq(
                                                new Unity.Mathematics.float2(stopPos.x, stopPos.z), 
                                                new Unity.Mathematics.float2(oPos.x, oPos.z)
                                            );
                                            if (distSq <= 14400.0f) // 120m
                                            {
                                                isNearby = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (isNearby)
                                    {
                                        var oPrefabRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(otherEntity);
                                        var oPrefab = prefabSystem.GetPrefab<Game.Prefabs.TransportLinePrefab>(oPrefabRef.m_Prefab);
                                        string oTypeStr = oPrefab != null ? oPrefab.m_TransportType.ToString().ToLower() : "bus";

                                        string oName;
                                        if (nameSystem.TryGetCustomName(otherEntity, out string oCustomName) && !string.IsNullOrEmpty(oCustomName))
                                        {
                                            oName = oCustomName;
                                        }
                                        else if (EntityManager.TryGetComponent<Game.Routes.RouteNumber>(otherEntity, out var oNum))
                                        {
                                            string oTypeDisplay = oPrefab != null ? oPrefab.m_TransportType.ToString() : "Route";
                                            oName = oNum.m_Number == 0 ? $"{oTypeDisplay} {otherEntity.Index}" : $"{oTypeDisplay} {oNum.m_Number}";
                                        }
                                        else
                                        {
                                            oName = "Route";
                                        }

                                        var oColorComp = EntityManager.GetComponentData<Game.Routes.Color>(otherEntity);
                                        string oColorHex = string.Format("#{0:X2}{1:X2}{2:X2}", oColorComp.m_Color.r, oColorComp.m_Color.g, oColorComp.m_Color.b);

                                        string safeOName = oName?.Replace("\"", "\\\"") ?? "Route";
                                        if (!firstNearby) nearbyLinesJson.Append(",");
                                        nearbyLinesJson.Append($@"{{""id"": {otherEntity.Index}, ""name"": ""{safeOName}"", ""color"": ""{oColorHex}"", ""type"": ""{oTypeStr}""}}");
                                        firstNearby = false;
                                    }
                                }
                            }
                        }
                        nearbyLinesJson.Append("]");

                        if (!firstStop) stopsJson.Append(",");
                        stopsJson.Append($@"{{""id"": {wp.Index}, ""targetId"": {stopTarget.Index}, ""name"": ""{safeStopName}"", ""waiting"": {stopWaiting}, ""waitTime"": {scaledWaitMin}, ""connectingLines"": {connLinesJson.ToString()}, ""nearbyLines"": {nearbyLinesJson.ToString()}}}");
                        firstStop = false;
                    }
                }
                stopsJson.Append("]");

                int avgWaitTime = stopsWithWaitCount > 0 ? (int)System.Math.Round(totalWaitTimeSum / stopsWithWaitCount) : 0;

                bool isCargo = false;
                if (EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(prefabRef.m_Prefab, out var lineData))
                {
                    isCargo = lineData.m_CargoTransport;
                }

                if (!first) result.Append(",");
                string safeName = name?.Replace("\"", "\\\"") ?? "Unnamed Route";
                result.Append($@"{{""id"": {entity.Index}, ""type"": ""{type}"", ""name"": ""{safeName}"", ""color"": ""{colorHex}"", ""vehicles"": {vehicles}, ""isDispatching"": {isDispatching.ToString().ToLower()}, ""hasShortage"": {hasShortage.ToString().ToLower()}, ""passengers"": {cargo}, ""waitingPassengers"": {totalWaitingPassengers}, ""avgWaitTime"": {avgWaitTime}, ""length"": ""{lengthStr}"", ""lengthRaw"": {length.ToString(System.Globalization.CultureInfo.InvariantCulture)}, ""usage"": {usage}, ""cargo"": {isCargo.ToString().ToLower()}, ""visible"": {isVisible.ToString().ToLower()}, ""stops"": {stops}, ""stopList"": {stopsJson.ToString()} }}");
                
                first = false;
            }
            
            result.Append("]");
            this.transitLinesDataBinding.Update(result.ToString());
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
                if (EntityManager.HasComponent<Game.Common.Owner>(targetEnt))
                {
                    Entity owner = EntityManager.GetComponentData<Game.Common.Owner>(targetEnt).m_Owner;
                    int depth = 0;
                    while (owner != Entity.Null && depth < 5)
                    {
                        depth++;
                        if (EntityManager.HasComponent<Game.Buildings.Building>(owner))
                            return owner;
                        if (EntityManager.HasComponent<Game.Common.Owner>(owner))
                            owner = EntityManager.GetComponentData<Game.Common.Owner>(owner).m_Owner;
                        else if (EntityManager.HasComponent<Game.Objects.Attached>(owner))
                            owner = EntityManager.GetComponentData<Game.Objects.Attached>(owner).m_Parent;
                        else
                            break;
                    }
                }

                // Check Attached Parent Building chain
                if (EntityManager.HasComponent<Game.Objects.Attached>(targetEnt))
                {
                    Entity parent = EntityManager.GetComponentData<Game.Objects.Attached>(targetEnt).m_Parent;
                    int depth = 0;
                    while (parent != Entity.Null && depth < 5)
                    {
                        depth++;
                        if (EntityManager.HasComponent<Game.Buildings.Building>(parent))
                            return parent;
                        if (EntityManager.HasComponent<Game.Objects.Attached>(parent))
                            parent = EntityManager.GetComponentData<Game.Objects.Attached>(parent).m_Parent;
                        else if (EntityManager.HasComponent<Game.Common.Owner>(parent))
                            parent = EntityManager.GetComponentData<Game.Common.Owner>(parent).m_Owner;
                        else
                            break;
                    }
                }
            }

            return Entity.Null;
        }

        private string GetStopResolvedName(Entity wp, Entity stopTarget, Game.UI.NameSystem nameSystem, int stopIndexCounter)
        {
            Entity displayEnt = stopTarget != Entity.Null ? stopTarget : wp;
            string baseName = null;

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
                    if (EntityManager.HasComponent<Game.Common.Owner>(targetEnt))
                    {
                        Entity owner = EntityManager.GetComponentData<Game.Common.Owner>(targetEnt).m_Owner;
                        if (owner != Entity.Null && !EntityManager.HasComponent<Game.Routes.Route>(owner))
                        {
                            if (nameSystem.TryGetCustomName(owner, out string ownerCustom) && !string.IsNullOrEmpty(ownerCustom))
                            {
                                baseName = ownerCustom;
                                break;
                            }

                            if (EntityManager.HasComponent<Game.Buildings.Building>(owner) && EntityManager.HasComponent<Game.Prefabs.PrefabRef>(owner))
                            {
                                var pRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(owner);
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
                    if (EntityManager.HasComponent<Game.Objects.Attached>(targetEnt))
                    {
                        Entity parent = EntityManager.GetComponentData<Game.Objects.Attached>(targetEnt).m_Parent;
                        if (parent != Entity.Null && !EntityManager.HasComponent<Game.Routes.Route>(parent))
                        {
                            if (nameSystem.TryGetCustomName(parent, out string parentCustom) && !string.IsNullOrEmpty(parentCustom))
                            {
                                baseName = parentCustom;
                                break;
                            }

                            if (EntityManager.HasComponent<Game.Buildings.Building>(parent) && EntityManager.HasComponent<Game.Prefabs.PrefabRef>(parent))
                            {
                                var pRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(parent);
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
                // (EXCLUDE Net Segments / Roads to prevent "Small Road" issue)
                if (string.IsNullOrEmpty(baseName))
                {
                    foreach (var targetEnt in targets)
                    {
                        if (targetEnt == Entity.Null) continue;

                        // CRITICAL FIX: Only check PrefabRef if it's a Building OR a TransportStop, NOT a Road/NetSegment (Edge/Node)
                        bool isBuilding = EntityManager.HasComponent<Game.Buildings.Building>(targetEnt);
                        bool isTransportStop = EntityManager.HasComponent<Game.Routes.TransportStop>(targetEnt);
                        bool isNetSegment = EntityManager.HasComponent<Game.Net.Edge>(targetEnt) || EntityManager.HasComponent<Game.Net.Node>(targetEnt);

                        if ((isBuilding || isTransportStop) && !isNetSegment && EntityManager.HasComponent<Game.Prefabs.PrefabRef>(targetEnt))
                        {
                            var pRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(targetEnt);
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