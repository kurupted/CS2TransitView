using Colossal.Entities;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Game.Routes;

namespace BetterTransitView.Systems
{
    public partial class BetterTransitViewPickerToolSystem : ToolBaseSystem
    {
        public override string toolID => "BetterTransitViewPickerTool";

        private TransitUISystem m_TransitUISystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TransitUISystem = World.GetOrCreateSystemManaged<TransitUISystem>();
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            
            // We only need to hit the ground or the network itself so we get a hit position
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain | TypeMask.Net;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_ToolSystem.activeTool != this) return inputDeps;

            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;

            // Handle canceling picker with Esc/Right-Click
            if (cancelAction.WasPressedThisFrame())
            {
                // Disable map picker and return to default tool
                m_TransitUISystem.CancelMapPicker();
                m_ToolSystem.activeTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
                return inputDeps;
            }

            // Handle clicking
            if (applyAction.WasReleasedThisFrame())
            {
                if ((m_ToolRaycastSystem.raycastFlags & RaycastFlags.UIDisable) == 0)
                {
                    if (GetRaycastResult(out Entity entity, out RaycastHit hit))
                    {
                        m_TransitUISystem.OnPickerClicked(hit.m_HitPosition);
                        
                        // Switch back to default tool after picking
                        m_ToolSystem.activeTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
                    }
                }
            }

            return inputDeps;
        }

        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;
    }
}
