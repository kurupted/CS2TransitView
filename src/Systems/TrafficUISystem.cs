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
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem m_ToolSystem;
        private Game.UI.InGame.InfoviewsUISystem m_InfoviewsUISystem;

        // UI Bindings
        private ValueBinding<bool> showTransitPanelBinding;
        private ValueBinding<string> transitLinesDataBinding;
        private ValueBinding<bool> showStopsAndStationsBinding;
        private ValueBinding<bool> showInfoviewBackgroundBinding;

        // Queries & Entities
        private EntityQuery m_TransitLinesQuery;
        private EntityQuery m_TransportLinePrefabQuery;
        private Game.Prefabs.InfoviewPrefab m_CustomInfoview;
        private Entity m_CustomInfoviewEntity = Entity.Null;
        
        // State
        public bool IsTransitPanelActive => this.showTransitPanelBinding?.value ?? false;
        private string m_ActiveTransitMode = "none";
        private string m_PendingTransitMode = "none";
        private bool m_ModeChangeRequested = false;
        private int m_TransitUpdateFrame = 0;
        private bool m_TransitLinesDirty = false;

        // Public Statics for the Render Jobs
        public static HashSet<Entity> HiddenCustomRoutes = new HashSet<Entity>();
        public static bool ShowStopsAndStations = true; 
        public static bool ShowInfoviewBackground = true; 

        protected override void OnCreate()
        {
            base.OnCreate();
            
            // Register this as a UI section
            m_InfoUISystem.AddMiddleSection(this);

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_InfoviewsUISystem = World.GetOrCreateSystemManaged<Game.UI.InGame.InfoviewsUISystem>();

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

            // Initialize Bindings
            this.showTransitPanelBinding = new ValueBinding<bool>("BetterTransitView", "showTransitPanel", false);
            this.transitLinesDataBinding = new ValueBinding<string>("BetterTransitView", "transitLinesData", "[]");
            this.showStopsAndStationsBinding = new ValueBinding<bool>("BetterTransitView", "showStopsAndStations", true);
            this.showInfoviewBackgroundBinding = new ValueBinding<bool>("BetterTransitView", "showInfoviewBackground", true);
            
            AddBinding(this.showTransitPanelBinding);
            AddBinding(this.transitLinesDataBinding);
            AddBinding(this.showStopsAndStationsBinding);
            AddBinding(this.showInfoviewBackgroundBinding);

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

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowInfoviewBackground", (show) => {
                ShowInfoviewBackground = show;
                this.showInfoviewBackgroundBinding.Update(show);
    
                if (this.IsTransitPanelActive) {
                    if (show && m_CustomInfoviewEntity != Entity.Null) {
                        m_InfoviewsUISystem.SetActiveInfoview(m_CustomInfoviewEntity);
                    } else {
                        m_InfoviewsUISystem.SetActiveInfoview(Entity.Null);
                    }
                }
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
                }
            }));

            AddBinding(new TriggerBinding<int>("BetterTransitView", "showVanillaLineInfo", (entityIndex) => {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in entities)
                {
                    if (e.Index == entityIndex)
                    {
                        m_ToolSystem.selected = e;
                        break;
                    }
                }
            }));
        }

        protected override string group => "BetterTransitView.Systems.TrafficUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected override void OnUpdate()
        {
            if (!Enabled) Enabled = true;
            
            // 1. Process Mode Changes Safely on the Main Thread
            if (m_ModeChangeRequested)
            {
                if (m_PendingTransitMode == "none") DeactivateTransitMode();
                else ActivateTransitMode(m_PendingTransitMode);
                m_ModeChangeRequested = false;
            }

            // 2. Transit Panel Logic Loop
            if (this.IsTransitPanelActive)
            {
                SyncVanillaVisibilityToUI();
                
                m_TransitUpdateFrame++;
                // Update data every 60 frames OR instantly if the dirty flag was tripped by a click
                if (m_TransitUpdateFrame % 60 == 0 || m_TransitLinesDirty) 
                {
                    UpdateTransitLinesData();
                    m_TransitLinesDirty = false;
                }
            }
            
            base.OnUpdate();
        }

        private void UpdateTransitLinesData()
        {
            if (!this.IsTransitPanelActive) return;

            using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var result = new System.Text.StringBuilder("[");
            var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
            var nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            bool first = true;

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

                string name = nameSystem.GetRenderedLabelName(entity);
                if (string.IsNullOrEmpty(name) || name.StartsWith("Assets."))
                {
                    if (EntityManager.TryGetComponent<Game.Routes.RouteNumber>(entity, out var routeNum))
                    {
                        int num = routeNum.m_Number;
                        name = num == 0 ? $"{displayType} {entity.Index}" : $"{displayType} {num}";
                    }
                    else name = "Unnamed Route";
                }

                var colorComp = EntityManager.GetComponentData<Game.Routes.Color>(entity); 
                string colorHex = string.Format("#{0:X2}{1:X2}{2:X2}", colorComp.m_Color.r, colorComp.m_Color.g, colorComp.m_Color.b);
                
                int cargo = 0;
                int capacity = 0;
                int vehicles = TransportUIUtils.GetRouteVehiclesCount(EntityManager, entity, ref cargo, ref capacity);
                float length = TransportUIUtils.GetRouteLength(EntityManager, entity);
                int usage = capacity > 0 ? UnityEngine.Mathf.RoundToInt(((float)cargo / capacity) * 100) : 0;
                string lengthStr = (length / 1000f).ToString("0.1") + "km";
                
                int stops = 0;
                if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<Game.Routes.RouteWaypoint> waypoints))
                {
                    stops = waypoints.Length;
                }

                bool isCargo = false;
                if (EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(prefabRef.m_Prefab, out var lineData))
                {
                    isCargo = lineData.m_CargoTransport;
                }

                if (!first) result.Append(",");
                string safeName = name?.Replace("\"", "\\\"") ?? "Unnamed Route";
                result.Append($@"{{""id"": {entity.Index}, ""type"": ""{type}"", ""name"": ""{safeName}"", ""color"": ""{colorHex}"", ""vehicles"": {vehicles}, ""passengers"": {cargo}, ""length"": ""{lengthStr}"", ""lengthRaw"": {length.ToString(System.Globalization.CultureInfo.InvariantCulture)}, ""usage"": {usage}, ""cargo"": {isCargo.ToString().ToLower()}, ""visible"": {isVisible.ToString().ToLower()}, ""stops"": {stops} }}");
                
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
            using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach(var e in entities) {
                if (!EntityManager.HasComponent<Game.Prefabs.PrefabRef>(e)) continue;
                var prefabRef = EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(e);
                if (EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(prefabRef.m_Prefab, out var lineData)) {
                    // Hide Cargo AND Airplanes by default
                    if (lineData.m_CargoTransport || lineData.m_TransportType == Game.Prefabs.TransportType.Airplane) {
                        HiddenCustomRoutes.Add(e);
                    }
                }
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
    }
}