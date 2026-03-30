using Colossal;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Input;
using Game.Net;
using Game.Objects;
using Game.Routes;
using Game.Prefabs; // for GrayWorld
using System.Reflection; // for GrayWorld
using Game.Pathfind;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using BetterTransitView.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Diagnostics;
using Entity = Unity.Entities.Entity;
using UnityEngine;
using Transform = Game.Objects.Transform;

namespace BetterTransitView.Systems
{
    public enum TrafficType
    {
        Citizen,
        Cargo,
        PublicTransport,
        Service
    }

    public struct TrafficRenderData
    {
        public Entity entity;
        public Entity sourceAgent; // The agent responsible for this entry (for distance checks)
        public Entity destinationEntity; 
        public Entity waitingAtStop;
        public Game.Citizens.Purpose purpose;
        public TrafficType type;
        public bool isOrigin;
        public bool isVehicle;
        public bool isPedestrian;
        public bool isDestination;
        public bool isMovingIn;
        public bool isTourist;
    }
    
    public enum BetterTransitViewStatusType
    {
        Stations = 101
    }

    [UpdateAfter(typeof(ToolSystem))]
    public partial class TrafficUISystem : InfoSectionBase
    {
        private ToolSystem toolSystem;
        private DefaultToolSystem defaultToolSystem;
        private BetterTransitViewToolSystem betterTransitViewToolSystem;
        private bool _isSpyModeActive = false;

        private ValueBinding<string> activityDataBinding;
        private ValueBinding<bool> toolActiveBinding;
        private ValueBinding<bool> highlightAgentsBinding;
        private ValueBinding<int> displayModeBinding; // 0 = Vehicles, 1 = Pedestrians
        private ValueBinding<bool> showRoutesBinding;
        private ValueBinding<int> directionModeBinding;
        private ValueBinding<int> rangeModeBinding; // 0=Short, 1=Med, 2=Long, 3=Unlimited
        private ValueBinding<string> associatedStopsBinding;
        private ValueBinding<bool> walkingOnlyBinding;
        private ValueBinding<bool> isTransitStopSelectedBinding;
        private ValueBinding<bool> hasParentBinding;

        private bool highlightAgents = false;
        private int displayMode = 0; // Default to Vehicles
        
        public static List<Entity> AnalyzedLanes = new List<Entity>();
        public bool HighlightAgents => highlightAgents;
        public bool ShowRoutes { get; private set; } = true; // Default to True
        private int directionMode = 0; // 0 = Both, 1 = Side A (Fwd), 2 = Side B (Bwd) 
        private int rangeMode = 1; // Default to Medium
        public int RangeMode => rangeMode;

        // Statics for the Route System to read
        public static float3 FilterPosition = float3.zero;
        public static float FilterDistance = 3000f; // Default Medium

        private bool isToolActive = false;
        private bool usePathBasedAnalysis = true;
        private bool walkingOnly = true;

        private bool wasToggleKeyDown = false;

        private List<TrafficRenderData> allAnalysisResults = new List<TrafficRenderData>();
        public static List<TrafficRenderData> CurrentRenderList = new List<TrafficRenderData>();
        public static bool IsDirty = false;

        private string currentFilter = "";
        private Entity lastSelectedEntity = Entity.Null;
        private Entity lastParentEntity = Entity.Null; 
        
        private EntityQuery pathOwnerQuery;
        private EntityQuery waitingPassengersQuery;
        
        private ValueBinding<bool> showTransitPanelBinding;
        private ValueBinding<string> transitLinesDataBinding;
        public Entity StationModeEntity { get; private set; } = Entity.Null;
        private bool m_TransitLinesDirty = false;
        
        private Entity m_VanillaInfoviewEntity = Entity.Null; // Caches the vanilla search so we don't query every click
        private Game.Prefabs.InfoviewPrefab m_CustomInfoview;
        private Entity m_CustomInfoviewEntity = Entity.Null;
        private string m_ActiveTransitMode = "none"; 
        
        private Game.UI.InGame.InfoviewsUISystem m_InfoviewsUISystem;
        public bool IsTransitPanelActive => this.showTransitPanelBinding?.value ?? false;
        private EntityQuery m_TransitLinesQuery;
        private int m_TransitUpdateFrame = 0;
        private EntityQuery m_TransportLinePrefabQuery;
        
        // --- Safe UI Communication Channels ---
        private string m_PendingTransitMode = "none";
        private bool m_ModeChangeRequested = false;
        public static HashSet<Entity> HiddenCustomRoutes = new HashSet<Entity>();
        private struct StopOption
        {
            public Entity Entity;
            public string Name;
        }
        private List<StopOption> m_AssociatedStops = new List<StopOption>();
        private EntityQuery m_AllStopsQuery;
        public static bool ShowStopsAndStations = true; 
        private ValueBinding<bool> showStopsAndStationsBinding;
        public static bool ShowInfoviewBackground = true; 
        private ValueBinding<bool> showInfoviewBackgroundBinding;
        
        
        protected override void OnCreate()
        {
            base.OnCreate();
            m_InfoUISystem.AddMiddleSection(this);

            this.toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            this.defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            this.betterTransitViewToolSystem = World.GetOrCreateSystemManaged<BetterTransitViewToolSystem>();
            
            // Listen for tool changes
            this.toolSystem.EventToolChanged += OnToolChanged;
            
            this.pathOwnerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.waitingPassengersQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] 
                {
                    ComponentType.ReadOnly<Game.Creatures.Resident>(),
                    ComponentType.ReadOnly<Creature>(),
                    ComponentType.ReadOnly<Target>(), 
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.m_AllStopsQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Routes.TransportStop>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            this.activityDataBinding = new ValueBinding<string>("BetterTransitView", "activityData", "{}");
            this.toolActiveBinding = new ValueBinding<bool>("BetterTransitView", "toolActive", false);
            this.highlightAgentsBinding = new ValueBinding<bool>("BetterTransitView", "highlightAgents", false);
            this.displayModeBinding = new ValueBinding<int>("BetterTransitView", "displayMode", 0);
            this.showRoutesBinding = new ValueBinding<bool>("BetterTransitView", "showRoutes", true);
            this.directionModeBinding = new ValueBinding<int>("BetterTransitView", "directionMode", 0);
            this.rangeModeBinding = new ValueBinding<int>("BetterTransitView", "rangeMode", 1);
            this.associatedStopsBinding = new ValueBinding<string>("BetterTransitView", "associatedStops", "[]");
            this.walkingOnlyBinding = new ValueBinding<bool>("BetterTransitView", "walkingOnly", true);
            this.isTransitStopSelectedBinding = new ValueBinding<bool>("BetterTransitView", "isTransitStopSelected", false);
            this.hasParentBinding = new ValueBinding<bool>("BetterTransitView", "hasParent", false);

            AddBinding(this.activityDataBinding);
            AddBinding(this.toolActiveBinding);
            AddBinding(this.highlightAgentsBinding);
            AddBinding(this.displayModeBinding);
            AddBinding(this.showRoutesBinding);
            AddBinding(this.directionModeBinding);
            AddBinding(this.rangeModeBinding);
            AddBinding(this.associatedStopsBinding);
            AddBinding(this.walkingOnlyBinding);
            AddBinding(this.isTransitStopSelectedBinding);
            AddBinding(this.hasParentBinding);

            AddBinding(new TriggerBinding<Entity>("BetterTransitView", "selectStop", (entity) => {
                this.toolSystem.selected = entity; 
            }));
            
            AddBinding(new TriggerBinding("BetterTransitView", "selectParent", () => {
                if (lastParentEntity != Entity.Null && EntityManager.Exists(lastParentEntity)) {
                    this.toolSystem.selected = lastParentEntity;
                }
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "sethighlightAgents", (bool active) => {
                this.highlightAgents = active;
                this.highlightAgentsBinding.Update(active);
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<int>("BetterTransitView", "setDisplayMode", (int mode) => {
                this.displayMode = mode;
                this.displayModeBinding.Update(mode);
                CalculateStats();
                ApplyFilter();
            }));
            
            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowRoutes", (bool active) => {
                this.ShowRoutes = active;
                this.showRoutesBinding.Update(active);
                // We set IsDirty to true so the RouteSystem knows to check immediately
                IsDirty = true; 
                ApplyFilter();
            }));

            // Handler for setting direction mode
            AddBinding(new TriggerBinding<int>("BetterTransitView", "setDirectionMode", (int mode) => {
                this.directionMode = mode;
                this.directionModeBinding.Update(mode);
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));

            // Handler for setting range mode
            AddBinding(new TriggerBinding<int>("BetterTransitView", "setRangeMode", (int mode) => {
                this.rangeMode = mode;
                this.rangeModeBinding.Update(mode);
                UpdateRangeDistance();
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));

            AddBinding(new TriggerBinding<string>("BetterTransitView", "setTrafficFilter", (string filter) => {
                if (filter == "RESET" || string.IsNullOrEmpty(filter)) this.currentFilter = "";
                else if (this.currentFilter == filter) this.currentFilter = "";
                else this.currentFilter = filter;
                ApplyFilter();
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setWalkingOnly", (bool active) => {
                this.walkingOnly = active;
                this.walkingOnlyBinding.Update(active);
                if (lastSelectedEntity != Entity.Null)
                {
                    RunAnalysis(lastSelectedEntity);
                }
            }));
            
            
            m_InfoviewsUISystem = World.GetOrCreateSystemManaged<Game.UI.InGame.InfoviewsUISystem>();
            
            SetupCustomInfoview();
            
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
            
            this.showTransitPanelBinding = new ValueBinding<bool>("BetterTransitView", "showTransitPanel", false);
            this.transitLinesDataBinding = new ValueBinding<string>("BetterTransitView", "transitLinesData", "[]");
            AddBinding(this.showTransitPanelBinding);
            AddBinding(this.transitLinesDataBinding);
            

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

                        m_TransitLinesDirty = true; // Trip the flag instead of rebuilding JSON immediately
                        break;
                    }
                }
            }));

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setAllLinesVisible", (show) => {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                //var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
                
                foreach (var e in entities)
                {
                    // Skip Airplanes so the Master Toggle ignores them
                    /*if (EntityManager.TryGetComponent<Game.Prefabs.PrefabRef>(e, out var prefabRef) &&
                        EntityManager.TryGetComponent<Game.Prefabs.TransportLineData>(prefabRef.m_Prefab, out var lineData))
                    {
                        if (lineData.m_TransportType == Game.Prefabs.TransportType.Airplane) continue;
                    }*/

                    if (show) HiddenCustomRoutes.Remove(e);
                    else HiddenCustomRoutes.Add(e);
                }
                m_TransitLinesDirty = true;
            }));
            
            this.showStopsAndStationsBinding = new ValueBinding<bool>("BetterTransitView", "showStopsAndStations", true);
            AddBinding(this.showStopsAndStationsBinding);

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setShowStopsAndStations", (show) => {
                ShowStopsAndStations = show;
                this.showStopsAndStationsBinding.Update(show);
            }));
            
            this.showInfoviewBackgroundBinding = new ValueBinding<bool>("BetterTransitView", "showInfoviewBackground", true);
            AddBinding(this.showInfoviewBackgroundBinding);

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
                var toolSystem = World.GetOrCreateSystemManaged<Game.Tools.ToolSystem>();
    
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
                    // Safely ask the ToolSystem to equip the prefab and switch to its default tool
                    toolSystem.ActivatePrefabTool(selectedPrefab);
                }
            }));
            
            // Opens the vanilla info panel for a specific route
            AddBinding(new TriggerBinding<int>("BetterTransitView", "showVanillaLineInfo", (entityIndex) => {
                using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in entities)
                {
                    if (e.Index == entityIndex)
                    {
                        // Setting the selected entity automatically opens its vanilla info panel
                        toolSystem.selected = e;
                        break;
                    }
                }
            }));
            
            string mockTransitData = @"[
                { ""id"": 1, ""type"": ""bus"", ""name"": ""My Route A"", ""color"": ""#4287f5"", ""vehicles"": 3, ""passengers"": 123, ""length"": ""12km"", ""usage"": 85, ""stops"": 10 },
                { ""id"": 2, ""type"": ""train"", ""name"": ""Express Line"", ""color"": ""#e67e22"", ""vehicles"": 2, ""passengers"": 450, ""length"": ""45km"", ""usage"": 92, ""stops"": 10 }
            ]";
            this.transitLinesDataBinding.Update(mockTransitData);

            AddBinding(new TriggerBinding<bool>("BetterTransitView", "setToolActive", SetToolActive));
        }

        protected override string group => "BetterTransitView.Systems.TrafficUISystem";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }

        protected bool ShouldBeVisible(Entity entity)
        {
            if (EntityManager.HasComponent<Game.Routes.TransportStop>(entity)) return true;
            if (EntityManager.HasComponent<Game.Buildings.TransportStation>(entity)) return true;

            return EntityManager.Exists(entity)
                   && EntityManager.HasBuffer<Game.Net.SubLane>(entity)
                   && !EntityManager.HasComponent<Building>(entity) 
                   && (EntityManager.HasComponent<Game.Net.Edge>(entity) || EntityManager.HasComponent<Game.Net.Node>(entity));
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (this.toolSystem != null)
            {
                this.toolSystem.EventToolChanged -= OnToolChanged;
            }
        }

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

            // 1. CHECK KEYBOARD INPUT
            if (Mod.m_ToggleAction != null)
            {
                bool isPressed = Mod.m_ToggleAction.IsPressed();
                // Only trigger if pressed NOW but wasn't pressed LAST frame
                if (isPressed && !wasToggleKeyDown) SetToolActive(!isToolActive);
                wasToggleKeyDown = isPressed;
            }
            
            if (this.IsTransitPanelActive)
            {
                // NEW: Force vanilla routes to respect your UI while the panel is open.
                // This instantly re-hides them if the vanilla route tool tries to show them.
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
            
            // Auto-Reactivate Tool
            if (_isSpyModeActive)
            {
                // If we are currently in Default Tool (viewing info or idle)
                if (toolSystem.activeTool == defaultToolSystem && toolSystem.selected == Entity.Null)
                {
                    // And nothing is selected (User just pressed Esc to close Info Panel)
                    // Reactivate the Spy Tool immediately
                    betterTransitViewToolSystem.Enable();
                }
            }

            Entity selected = this.toolSystem.selected;

            if (ShouldBeVisible(selected))
            {
                this.visible = true;
            }
            else
            {
                this.visible = false;
                ClearData();
                return;
            }

            if (selected != lastSelectedEntity)
            {
                bool isDirectStop = EntityManager.HasComponent<Game.Routes.TransportStop>(selected);
                
                // Track Parent Entity for the "Back" button
                if (!isDirectStop) lastParentEntity = selected;
                this.hasParentBinding.Update(isDirectStop && lastParentEntity != Entity.Null && EntityManager.Exists(lastParentEntity));

                lastSelectedEntity = selected;
                currentFilter = "";

                // Reset direction to All Sides (0)
                this.directionMode = 0;
                this.directionModeBinding.Update(0);
                
                bool isStopOrStation = isDirectStop || EntityManager.HasComponent<Game.Buildings.TransportStation>(selected);
                this.isTransitStopSelectedBinding.Update(isStopOrStation);

                FindAssociatedStops(selected);
                RunAnalysis(selected);
            }
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
                    HiddenCustomRoutes.Add(entity); // RenderJob will skip it
                    continue; // UI will skip it
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
            // Removed m_LegendType assignment
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
            
        private void ActivateTransitMode(string mode)
        {
            m_ActiveTransitMode = mode;
            this.showTransitPanelBinding.Update(true);
            
            HiddenCustomRoutes.Clear();
            using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var prefabSystem = World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>();
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
        
        
        private void SyncVanillaVisibilityToUI()
        {
            if (m_TransitLinesQuery.IsEmptyIgnoreFilter) return;

            using var entities = m_TransitLinesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            bool needsUpdate = false;

            // Pass 1: Quick check to see if ANY route is out of sync
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

            // Pass 2: If everything matches, exit immediately (No ECB allocation = No lag)
            if (!needsUpdate) return;

            // Pass 3: Something is out of sync, safely apply the changes
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

        
        private void OnToolChanged(ToolBaseSystem newTool)
        {
            if (_isSpyModeActive)
            {
                if (newTool != betterTransitViewToolSystem && newTool != defaultToolSystem)
                {
                    SetSpyMode(false);
                }
            }
        }

        public void SetSpyMode(bool active)
        {
            if (_isSpyModeActive != active)
            {
                _isSpyModeActive = active;
                this.isToolActive = active;
                this.toolActiveBinding.Update(active);

                if (active) betterTransitViewToolSystem.Enable();
                else
                {
                    // If disabling, ensure we switch back to default tool if we were spying
                    if (toolSystem.activeTool == betterTransitViewToolSystem) betterTransitViewToolSystem.Disable();
                    ClearData();
                }
            }
        }
        
        private void SetToolActive(bool active) => SetSpyMode(active);
        public void SyncToolState(bool active) => SetSpyMode(active);
        
        private void ClearData()
        {
            lastSelectedEntity = Entity.Null;
            lastParentEntity = Entity.Null;
            currentFilter = "";
            FilterPosition = float3.zero;
            if (this.activityDataBinding.value != "{}")
            {
                this.activityDataBinding.Update("{}");
                allAnalysisResults.Clear();
                CurrentRenderList.Clear();
                AnalyzedLanes.Clear(); // Clear lanes
                IsDirty = true;
                this.associatedStopsBinding.Update("[]"); 
                this.hasParentBinding.Update(false);
            }
        }

        private void UpdateRangeDistance()
        {
            switch (rangeMode)
            {
                case 0: FilterDistance = float.MaxValue; break; // Lane Data Only (distance doesn't matter, handled by job skipping PathElements)
                case 1: FilterDistance = 1000f; break; // 1km
                case 2: FilterDistance = 2000f; break; // 2km
                case 3: FilterDistance = float.MaxValue; break; // Unlimited
                default: FilterDistance = 2000f; break;
            }
        }
        
        private bool IsItemVisible(TrafficRenderData item, ComponentLookup<Game.Objects.Transform> transformLookup)
        {
            bool isPed = item.isPedestrian;

            bool isStopOrStation = lastSelectedEntity != Entity.Null && 
                                  (EntityManager.HasComponent<Game.Routes.TransportStop>(lastSelectedEntity) || 
                                   EntityManager.HasComponent<Game.Buildings.TransportStation>(lastSelectedEntity));

            // Strictly enforce pedestrian/passenger mode when viewing a stop or station
            if (isStopOrStation)
            {
                if (!isPed) return false; 
            }
            else
            {
                // Display Mode Check
                if (this.displayMode == 0 && !item.isVehicle) return false; 
                if (this.displayMode == 1 && !isPed) return false; 
                if (this.displayMode == 1 && this.walkingOnly && item.waitingAtStop != Entity.Null) return false;
            }

            if (item.sourceAgent == Entity.Null) return true;

            // Range Check
            if (!isStopOrStation && FilterDistance < 1000000f)
            {
                Entity entityToCheck = item.sourceAgent != Entity.Null ? item.sourceAgent : item.entity;
                if (transformLookup.TryGetComponent(entityToCheck, out Game.Objects.Transform trans))
                {
                    if (math.distancesq(trans.m_Position, FilterPosition) > (FilterDistance * FilterDistance))
                        return false;
                }
                else 
                {
                    // unspawned / in a building have no transform
                    // Return false here so they get excluded if range isn't unlimited.
                    return false;
                }
            }
            return true;
        }

        private void ApplyFilter()
        {
            CurrentRenderList.Clear();
            if (allAnalysisResults == null) return;
            ComponentLookup<Game.Objects.Transform> transformLookup = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true);

            foreach (var item in allAnalysisResults)
            {
                // Use centralized visibility check
                if (!IsItemVisible(item, transformLookup)) continue;

                bool matchesFilter = false;
                if (string.IsNullOrEmpty(currentFilter)) matchesFilter = true;
                else matchesFilter = MatchesFilter(item, currentFilter);

                if (!matchesFilter) continue;

                if (item.isDestination) CurrentRenderList.Add(item);
                else if (!string.IsNullOrEmpty(currentFilter) || this.highlightAgents || this.ShowRoutes) CurrentRenderList.Add(item);
            }
            IsDirty = true;
        }

        private bool MatchesFilter(TrafficRenderData item, string filter)
        {
            switch (filter)
            {
                case "none": return item.purpose == Purpose.None && item.type != TrafficType.Service && item.type != TrafficType.Cargo && item.type != TrafficType.PublicTransport;
                case "shopping": return item.purpose == Purpose.Shopping;
                case "leisure": return item.purpose == Purpose.Leisure || item.purpose == Purpose.Relaxing || item.purpose == Purpose.Sleeping || item.purpose == Purpose.WaitingHome;
                case "goingHome": return item.purpose == Purpose.GoingHome && !item.isTourist;
                case "goingToWork": return item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working;
                case "movingIn": return item.isMovingIn;
                case "movingAway": return item.purpose == Purpose.MovingAway && !item.isTourist;
                case "school": return item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying;
                case "transporting": return (item.type == TrafficType.Cargo && item.purpose == Purpose.Delivery) || (item.type == TrafficType.Citizen && (item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping));
                case "returning": return item.type == TrafficType.Cargo && item.purpose == Purpose.None;
                case "tourism": 
                    // Includes standard tourism purposes OR Tourists going home (leaving city)
                    return item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions || ((item.purpose == Purpose.GoingHome || item.purpose == Purpose.MovingAway) && item.isTourist);  
                case "services": return item.type == TrafficType.Service || item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital || item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage || item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail || item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail;
                case "other":
                    if (item.type == TrafficType.Cargo || item.type == TrafficType.Service) return false;
                    if (item.type == TrafficType.PublicTransport) return true;
                    return item.type == TrafficType.Citizen && item.purpose != Purpose.None && item.purpose != Purpose.Shopping && !(item.purpose == Purpose.Leisure || item.purpose == Purpose.Relaxing || item.purpose == Purpose.Sleeping || item.purpose == Purpose.WaitingHome) && item.purpose != Purpose.GoingHome && !(item.purpose == Purpose.GoingToWork || item.purpose == Purpose.Working) && item.purpose != Purpose.MovingAway && !(item.purpose == Purpose.GoingToSchool || item.purpose == Purpose.Studying) && !(item.purpose == Purpose.Sightseeing || item.purpose == Purpose.Traveling || item.purpose == Purpose.VisitAttractions) && !(item.purpose == Purpose.Delivery || item.purpose == Purpose.Exporting || item.purpose == Purpose.UpkeepDelivery || item.purpose == Purpose.StorageTransfer || item.purpose == Purpose.Collect || item.purpose == Purpose.CompanyShopping) && !(item.purpose == Purpose.Hospital || item.purpose == Purpose.InHospital || item.purpose == Purpose.Deathcare || item.purpose == Purpose.ReturnGarbage || item.purpose == Purpose.InDeathcare || item.purpose == Purpose.ReturnUnsortedMail || item.purpose == Purpose.ReturnLocalMail || item.purpose == Purpose.ReturnOutgoingMail || item.purpose == Purpose.SendMail);
                default: return false;
            }
        }
        
        private NativeHashSet<Entity> GetTargetEntities(Entity segment, Allocator allocator, int directionMode)
        {
            NativeHashSet<Entity> targets = new NativeHashSet<Entity>(16, allocator);
            
            if (directionMode == 0 || EntityManager.HasComponent<Game.Net.Node>(segment))
            {
                targets.Add(segment);

                // 1. MACRO-PATHFINDING (Highways & Long Distances)
                // CS2 groups highways into "Aggregate" entities. Cars far away may target this aggregate instead of the specific micro-segment
                if (EntityManager.HasComponent<Game.Net.Aggregated>(segment))
                {
                    Game.Net.Aggregated aggregated = EntityManager.GetComponentData<Game.Net.Aggregated>(segment);
                    if (aggregated.m_Aggregate != Entity.Null)
                    {
                        targets.Add(aggregated.m_Aggregate);
                    }
                }
            }
            
            // Get the main road segment's geometry
            if (EntityManager.TryGetBuffer(segment, true, out DynamicBuffer<Game.Net.SubLane> lanes))
            {
                // EDGE CASE (Standard Road Segment)
                if (EntityManager.HasComponent<Curve>(segment))
                {
                    Curve segmentCurve = EntityManager.GetComponentData<Curve>(segment);
                    // Calculate the "Center" and "Right Vector" of the road at the midpoint (t=0.5)
                    // Position(0.5)
                    float3 segmentPos = Colossal.Mathematics.MathUtils.Position(segmentCurve.m_Bezier, 0.5f);
                    // Tangent(0.5) gives the forward direction
                    float3 segmentTan = Colossal.Mathematics.MathUtils.Tangent(segmentCurve.m_Bezier, 0.5f);
                    // Cross product with Up (0,1,0) gives the Right Vector
                    float3 segmentRight = math.cross(segmentTan, new float3(0, 1, 0));

                    for (int i = 0; i < lanes.Length; i++)
                    {
                        Entity subLaneEntity = lanes[i].m_SubLane;
                        if (directionMode != 0)
                        {
                            // We check the geometry of the sub-lane
                            if (EntityManager.HasComponent<Curve>(subLaneEntity))
                            {
                                Curve laneCurve = EntityManager.GetComponentData<Curve>(subLaneEntity);
                                float3 lanePos = Colossal.Mathematics.MathUtils.Position(laneCurve.m_Bezier, 0.5f);
                                
                                // Calculate vector from Road Center -> Lane Center
                                float3 diff = lanePos - segmentPos;
                                
                                // Dot product determines side:
                                // > 0 means the lane is on the Right side of the road center
                                // < 0 means the lane is on the Left side of the road center
                                float dot = math.dot(diff, segmentRight);
                                
                                // Threshold 0.1f ignores tiny floating point errors for exact center lanes

                                // Mode 1 (Side A): We want ONE side (e.g. Left). So skip if dot is Positive (Right).
                                if (directionMode == 1 && dot > 0.1f) continue;
                                
                                // Mode 2 (Side B): We want OTHER side (e.g. Right). So skip if dot is Negative (Left).
                                if (directionMode == 2 && dot < -0.1f) continue;
                            }
                        }
                        targets.Add(subLaneEntity);
                    }
                }
                // NODE CASE (Intersection / Roundabout)
                else if (EntityManager.HasComponent<Game.Net.Node>(segment))
                {
                    // Nodes don't have a simple forward/backward direction.
                    // Ignore directionMode and just add all internal connection lanes.
                    for (int i = 0; i < lanes.Length; i++)
                    {
                        targets.Add(lanes[i].m_SubLane);
                    }
                }
            }
            return targets;
        }

        
        private bool IsValidTransitStop(Entity entity)
        {
            return EntityManager.HasComponent<Game.Routes.BusStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TrainStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TramStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.SubwayStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.ShipStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.AirplaneStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.FerryStop>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TaxiStand>(entity);
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
        
        private void FindAssociatedStops(Entity selected)
        {
            m_AssociatedStops.Clear();
            if (selected == Entity.Null) {
                this.associatedStopsBinding.Update("[]");
                return;
            }

            bool isDirectStop = EntityManager.HasComponent<Game.Routes.TransportStop>(selected);

            if (isDirectStop && IsValidTransitStop(selected))
            {
                m_AssociatedStops.Add(new StopOption { Entity = selected, Name = "" });
            }

            var stopEntities = m_AllStopsQuery.ToEntityArray(Allocator.Temp);
            
            NativeList<Entity> targetParts = new NativeList<Entity>(Allocator.Temp);
            targetParts.Add(selected);
            
            if (EntityManager.HasComponent<Game.Net.Edge>(selected))
            {
                Game.Net.Edge edge = EntityManager.GetComponentData<Game.Net.Edge>(selected);
                targetParts.Add(edge.m_Start);
                targetParts.Add(edge.m_End);
            }
            if (EntityManager.TryGetBuffer(selected, true, out DynamicBuffer<Game.Objects.SubObject> subObjs))
            {
                foreach(var sub in subObjs) targetParts.Add(sub.m_SubObject);
            }

            for (int i = 0; i < stopEntities.Length; i++)
            {
                Entity stopEntity = stopEntities[i];
                
                // Exclude bicycle parking and strictly enforce whitelist
                if (!IsValidTransitStop(stopEntity)) continue;

                Entity currentChecker = stopEntity;
                if (EntityManager.HasComponent<Game.Routes.Connected>(stopEntity))
                    currentChecker = EntityManager.GetComponentData<Game.Routes.Connected>(stopEntity).m_Connected;

                bool matchFound = false;
                
                for (int depth = 0; depth < 4; depth++)
                {
                    Entity nextChecker = Entity.Null;

                    if (EntityManager.HasComponent<Owner>(currentChecker))
                        nextChecker = EntityManager.GetComponentData<Owner>(currentChecker).m_Owner;
                    else if (EntityManager.HasComponent<Attached>(currentChecker))
                        nextChecker = EntityManager.GetComponentData<Attached>(currentChecker).m_Parent;

                    if (nextChecker != Entity.Null)
                    {
                        if (targetParts.Contains(nextChecker))
                        {
                            matchFound = true;
                            break;
                        }
                        currentChecker = nextChecker;
                    }
                    else break;
                }

                if (matchFound)
                {
                    bool exists = false;
                    foreach(var s in m_AssociatedStops) if(s.Entity == stopEntity) exists = true;
                    if (!exists) 
                    {
                        m_AssociatedStops.Add(new StopOption { Entity = stopEntity, Name = "" });
                    }
                }
            }
            targetParts.Dispose();
            
            // hide the UI stop list if the user has explicitly selected a single stop
            if (isDirectStop)
            {
                this.associatedStopsBinding.Update("[]");
            }
            else
            {
                // Sort by Entity Index so stops remain consistently in the exact same order visually
                m_AssociatedStops.Sort((a, b) => a.Entity.Index.CompareTo(b.Entity.Index));
                
                var sb = new System.Text.StringBuilder("[");
                for(int i = 0; i < m_AssociatedStops.Count; i++)
                {
                    var stop = m_AssociatedStops[i];
                    stop.Name = $"{GetStopPrefix(stop.Entity)} {i + 1}";
                    m_AssociatedStops[i] = stop; // save generated name
                    
                    sb.Append($"{{\"index\":{stop.Entity.Index}, \"version\":{stop.Entity.Version}, \"name\":\"{stop.Name}\"}},");
                }
                if (m_AssociatedStops.Count > 0) sb.Length--; 
                sb.Append("]");
                
                this.associatedStopsBinding.Update(sb.ToString());
            }
        }

        private void FindStopsRecursive(Entity entity, ref NativeHashSet<Entity> results)
        {
            if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<ConnectedRoute> routes))
            {
                foreach(var route in routes) results.Add(route.m_Waypoint);
            }
            if (EntityManager.TryGetBuffer(entity, true, out DynamicBuffer<Game.Objects.SubObject> subObjects))
            {
                foreach(var sub in subObjects) FindStopsRecursive(sub.m_SubObject, ref results);
            }
        }

        private void CalculateStats()
        {
            int cntNone = 0, cntShopping = 0, cntLeisure = 0, cntGoingHome = 0, cntGoingToWork = 0, cntMovingIn = 0, cntMovingAway = 0, cntSchool = 0, cntTransporting = 0, cntReturning = 0, cntTourism = 0, cntOther = 0, cntServices = 0;

            ComponentLookup<Game.Objects.Transform> transformLookup = SystemAPI.GetComponentLookup<Transform>(true);

            foreach (var item in allAnalysisResults)
            {
                // Use centralized visibility check (respects Range and DisplayMode)
                if (!IsItemVisible(item, transformLookup)) continue;
                if (item.isDestination) continue;

                if (item.type == TrafficType.Service) { cntServices++; continue; }
                if (item.type == TrafficType.PublicTransport) { cntOther++; continue; }
                if (item.type == TrafficType.Cargo) {
                    if (item.purpose == Purpose.Delivery) cntTransporting++;
                    else cntReturning++;
                    continue;
                }

                switch (item.purpose) {
                    case Purpose.None: cntNone++; break;
                    case Purpose.Shopping: cntShopping++; break;
                    case Purpose.Leisure:
                    case Purpose.Sleeping:
                    case Purpose.WaitingHome:
                    case Purpose.Relaxing: cntLeisure++; break;
                    case Purpose.GoingHome:
                        if (item.isMovingIn) cntMovingIn++;
                        else if (item.isTourist) cntTourism++; // Tourists leaving counted as Tourism
                        else cntGoingHome++;
                        break;
                    case Purpose.GoingToWork:
                    case Purpose.Working: cntGoingToWork++; break;
                    case Purpose.MovingAway:
                        if (item.isTourist) cntTourism++; 
                        else cntMovingAway++; 
                        break;
                    case Purpose.GoingToSchool:
                    case Purpose.Studying: cntSchool++; break;
                    case Purpose.Sightseeing:
                    case Purpose.Traveling:
                    case Purpose.VisitAttractions: cntTourism++; break;
                    case Purpose.Delivery:
                    case Purpose.Exporting:
                    case Purpose.UpkeepDelivery:
                    case Purpose.StorageTransfer:
                    case Purpose.Collect:
                    case Purpose.CompanyShopping: cntTransporting++; break;
                    case Purpose.ReturnGarbage:
                    case Purpose.Deathcare:
                    case Purpose.InDeathcare:
                    case Purpose.ReturnUnsortedMail:
                    case Purpose.ReturnLocalMail:
                    case Purpose.ReturnOutgoingMail:
                    case Purpose.SendMail:
                    case Purpose.Hospital:
                    case Purpose.InHospital: cntServices++; break;
                    default: cntOther++; break;
                }
            }

            string json = $@"{{""none"": {cntNone}, ""shopping"": {cntShopping}, ""leisure"": {cntLeisure}, ""goingHome"": {cntGoingHome}, ""goingToWork"": {cntGoingToWork}, ""movingIn"": {cntMovingIn}, ""movingAway"": {cntMovingAway}, ""school"": {cntSchool}, ""transporting"": {cntTransporting}, ""returning"": {cntReturning}, ""tourism"": {cntTourism}, ""other"": {cntOther}, ""services"": {cntServices}}}";
            this.activityDataBinding.Update(json);
        }

        private void RunAnalysis(Entity selectedSegment)
        {
            allAnalysisResults.Clear();
            AnalyzedLanes.Clear(); // Clear lanes
            
            // Calculate Center Position for Range Filtering
            if (EntityManager.HasComponent<Game.Net.Node>(selectedSegment)) {
                Game.Net.Node node = EntityManager.GetComponentData<Game.Net.Node>(selectedSegment);
                FilterPosition = node.m_Position;
            } else if (EntityManager.HasComponent<Curve>(selectedSegment)) {
                Curve curve = EntityManager.GetComponentData<Curve>(selectedSegment);
                FilterPosition = Colossal.Mathematics.MathUtils.Position(curve.m_Bezier, 0.5f);
            } else if (SystemAPI.GetComponentLookup<Game.Objects.Transform>(true).TryGetComponent(selectedSegment, out Game.Objects.Transform tr)) {
                FilterPosition = tr.m_Position;
            } else {
                FilterPosition = float3.zero;
            }
            UpdateRangeDistance();

            NativeQueue<TrafficRenderData> resultsQueue = new NativeQueue<TrafficRenderData>(Allocator.TempJob);

            NativeList<Entity> stopsToAnalyze = new NativeList<Entity>(Allocator.TempJob);
            
            bool isDirectStop = EntityManager.HasComponent<Game.Routes.TransportStop>(selectedSegment);
            bool isStation = EntityManager.HasComponent<Game.Buildings.TransportStation>(selectedSegment);

            // highlight the road segment or its specific lanes
            if (isDirectStop)
            {
                stopsToAnalyze.Add(selectedSegment);
                AnalyzedLanes.Add(selectedSegment); 
            }
            else if (isStation)
            {
                AnalyzedLanes.Add(selectedSegment); 
            }
            
            foreach(var option in m_AssociatedStops)
            {
                bool alreadyAdded = false;
                for(int k=0; k<stopsToAnalyze.Length; k++) if(stopsToAnalyze[k] == option.Entity) alreadyAdded = true;
                if(!alreadyAdded) stopsToAnalyze.Add(option.Entity);
            }

            bool shouldAnalyzeStops = stopsToAnalyze.Length > 0 && (isDirectStop || isStation || !this.walkingOnly);

            JobHandle waitingJobHandle = default;
            NativeList<int> debugList = new NativeList<int>(Allocator.TempJob);

            if (shouldAnalyzeStops)
            {
                Mod.log.Info($"BetterTransitView: Analyzing Queue for {stopsToAnalyze.Length} stops.");
                
                WaitingPassengerJob waitJob = new WaitingPassengerJob
                {
                    searchTargets = stopsToAnalyze.AsArray(),
                    debugList = debugList,
                    
                    queueBufferHandle = SystemAPI.GetBufferTypeHandle<Game.Creatures.Queue>(true), 
                    residentHandle = SystemAPI.GetComponentTypeHandle<Game.Creatures.Resident>(true),
                    
                    creatureHandle = SystemAPI.GetComponentTypeHandle<Creature>(true),
                    humanLaneHandle = SystemAPI.GetComponentTypeHandle<HumanCurrentLane>(true),
                    
                    entityHandle = SystemAPI.GetEntityTypeHandle(),
                    targetHandle = SystemAPI.GetComponentTypeHandle<Target>(true),
                    
                    connectedLookup = SystemAPI.GetComponentLookup<Game.Routes.Connected>(true),
                    travelPurposeLookup = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    householdMemberLookup = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                    householdLookup = SystemAPI.GetComponentLookup<Household>(true),
                    workerLookup = SystemAPI.GetComponentLookup<Worker>(true), 
                    studentLookup = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                    propertyRenterLookup = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    pathOwnerHandle = SystemAPI.GetComponentTypeHandle<PathOwner>(true),
                    pathBufferHandle = SystemAPI.GetBufferTypeHandle<PathElement>(true),
                    results = resultsQueue.AsParallelWriter()
                };
                waitingJobHandle = waitJob.Schedule(waitingPassengersQuery, default); 
            }

            JobHandle pathJobHandle = default;
            NativeHashSet<Entity> targets = default; 
            
            debugList.Dispose();

            if (targets.IsCreated) pathJobHandle.Complete();

            while (resultsQueue.TryDequeue(out TrafficRenderData item)) allAnalysisResults.Add(item);
            
            resultsQueue.Dispose();
            stopsToAnalyze.Dispose();
            if (targets.IsCreated) targets.Dispose();
            
            CalculateStats();
            ApplyFilter();
        }

    }

    [Unity.Burst.BurstCompile]
    public struct WaitingPassengerJob : IJobChunk
    {
        [ReadOnly] public NativeArray<Entity> searchTargets; 
        public NativeList<int> debugList; 
        
        [ReadOnly] public BufferTypeHandle<Game.Creatures.Queue> queueBufferHandle; 
        [ReadOnly] public ComponentTypeHandle<Game.Creatures.Resident> residentHandle;
        [ReadOnly] public ComponentTypeHandle<Target> targetHandle;
        [ReadOnly] public EntityTypeHandle entityHandle; 
        
        [ReadOnly] public ComponentTypeHandle<Creature> creatureHandle;
        [ReadOnly] public ComponentTypeHandle<HumanCurrentLane> humanLaneHandle;
        
        [ReadOnly] public ComponentLookup<Game.Routes.Connected> connectedLookup; 
        [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Household> householdLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;
        [ReadOnly] public ComponentTypeHandle<PathOwner> pathOwnerHandle;
        [ReadOnly] public BufferTypeHandle<PathElement> pathBufferHandle;
        
        public NativeQueue<TrafficRenderData>.ParallelWriter results;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            NativeArray<Game.Creatures.Resident> residents = chunk.GetNativeArray(ref residentHandle);
            NativeArray<Entity> entities = chunk.GetNativeArray(entityHandle); 
            
            NativeArray<Creature> creatures = chunk.GetNativeArray(ref creatureHandle);
            bool hasHumanLanes = chunk.Has(ref humanLaneHandle);
            NativeArray<HumanCurrentLane> humanLanes = hasHumanLanes ? chunk.GetNativeArray(ref humanLaneHandle) : default;
            
            bool hasQueue = chunk.Has(ref queueBufferHandle);
            BufferAccessor<Game.Creatures.Queue> queues = hasQueue ? chunk.GetBufferAccessor(ref queueBufferHandle) : default;

            bool hasPaths = chunk.Has(ref pathBufferHandle);
            BufferAccessor<PathElement> pathBuffers = hasPaths ? chunk.GetBufferAccessor(ref pathBufferHandle) : default;
            
            NativeArray<Target> targetComponents = chunk.GetNativeArray(ref targetHandle);

            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out int i))
            {
                Entity matchedStop = Entity.Null;

                // 1. Check Buffer-Based Queue (Game.Creatures.Queue)
                if (hasQueue)
                {
                    DynamicBuffer<Game.Creatures.Queue> myQueue = queues[i];
                    for (int q = 0; q < myQueue.Length; q++)
                    {
                        Entity intermediateEntity = myQueue[q].m_TargetEntity; 
                        
                        if (connectedLookup.TryGetComponent(intermediateEntity, out var connection))
                        {
                            Entity actualStop = connection.m_Connected;
                            for(int k=0; k<searchTargets.Length; k++) 
                            {
                                if (searchTargets[k] == actualStop || searchTargets[k] == intermediateEntity) 
                                {
                                    matchedStop = searchTargets[k];
                                    break;
                                }
                            }
                        }
                        if (matchedStop != Entity.Null) break;
                    }
                }

                // 2. Check Component-Based Queue (Game.Creatures.Creature.m_QueueEntity)
                if (matchedStop == Entity.Null)
                {
                    Entity queueEntity = creatures[i].m_QueueEntity;
                    if (queueEntity != Entity.Null)
                    {
                        Entity actualStop = queueEntity;
                        if (connectedLookup.TryGetComponent(queueEntity, out var connection))
                        {
                            actualStop = connection.m_Connected;
                        }

                        for(int k=0; k<searchTargets.Length; k++) 
                        {
                            if (searchTargets[k] == actualStop || searchTargets[k] == queueEntity) 
                            {
                                matchedStop = searchTargets[k];
                                break;
                            }
                        }
                    }
                }

                // 3. Fallback: If they have a queue entity (meaning they are waiting for transit), 
                // but it was a TransportLine instead of a stop, check if they are physically standing on the platform.
                if (matchedStop == Entity.Null && creatures[i].m_QueueEntity != Entity.Null && hasHumanLanes)
                {
                    Entity physicalLane = humanLanes[i].m_Lane;
                    for(int k=0; k<searchTargets.Length; k++) 
                    {
                        if (searchTargets[k] == physicalLane) 
                        {
                            matchedStop = searchTargets[k];
                            break;
                        }
                    }
                }

                // If ALL THREE failed, skip them
                if (matchedStop == Entity.Null) continue;

                Entity citizen = residents[i].m_Citizen;
                Purpose purpose = Purpose.None;
                bool isMovingIn = false;
                bool isTourist = false;
                Entity finalDestination = Entity.Null;

                if (travelPurposeLookup.TryGetComponent(citizen, out var tp)) purpose = tp.m_Purpose;

                if (purpose == Purpose.GoingToWork && workerLookup.TryGetComponent(citizen, out var worker))
                {
                    finalDestination = worker.m_Workplace;
                }
                else if ((purpose == Purpose.GoingToSchool || purpose == Purpose.Studying) && studentLookup.TryGetComponent(citizen, out var student))
                {
                    finalDestination = student.m_School;
                }
                else if (purpose == Purpose.GoingHome && householdMemberLookup.TryGetComponent(citizen, out var hm))
                {
                    if (propertyRenterLookup.TryGetComponent(hm.m_Household, out var renter))
                    {
                        finalDestination = renter.m_Property;
                        if (householdLookup.TryGetComponent(hm.m_Household, out var hh))
                        {
                            if ((hh.m_Flags & HouseholdFlags.MovedIn) == 0) isMovingIn = true;
                        }
                    }
                }

                if (finalDestination == Entity.Null)
                {
                    finalDestination = targetComponents[i].m_Target;
                }

                if (finalDestination == Entity.Null && hasPaths && i < pathBuffers.Length)
                {
                    DynamicBuffer<PathElement> path = pathBuffers[i];
                    if (path.Length > 0)
                    {
                        finalDestination = path[path.Length - 1].m_Target;
                    }
                }

                results.Enqueue(new TrafficRenderData
                {
                    entity = citizen,
                    sourceAgent = entities[i], 
                    destinationEntity = finalDestination, 
                    waitingAtStop = matchedStop, 
                    purpose = purpose,
                    type = TrafficType.Citizen,
                    isPedestrian = true,
                    isMovingIn = isMovingIn,
                    isTourist = isTourist
                });
            }
        }
    }
}