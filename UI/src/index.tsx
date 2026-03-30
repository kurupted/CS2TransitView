import { ModRegistrar } from "cs2/modding";
import { TransitPanel } from "./TransitPanel";
import { TrafficButton } from "./TrafficButton";
import { SelectedInfoPanelTogglesComponent } from "./SelectedInfoPanelTogglesComponent";
import { VanillaComponentResolver } from "./VanillaComponentResolver";

export default ((moduleRegistry) => {
    // 1. Setup Resolver (Critical for tool UI)
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // 2. Add Buttons & Panels
    moduleRegistry.append('GameTopLeft', TrafficButton);
    moduleRegistry.append('Game', TransitPanel);

    // 3. Re-extend the Info Panel (Fixes Issue #1)
    const infoPanelPath = "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx";
    const infoPanelExport = "selectedInfoSectionComponents";

    try {
        moduleRegistry.extend(
            infoPanelPath,
            infoPanelExport,
            SelectedInfoPanelTogglesComponent
        );
    } catch (e) {
        console.error("[BetterTransitView] Failed to register Info Panel extensions", e);
    }

}) as ModRegistrar;