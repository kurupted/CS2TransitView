import { ModRegistrar } from "cs2/modding";
import { TransitPanel } from "./TransitPanel";
import { TransitButton } from "./TransitButton";
import { VanillaComponentResolver } from "./VanillaComponentResolver";

export default ((moduleRegistry) => {
    // 1. Setup Resolver (Critical for tool UI)
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // 2. Add Buttons & Panels
    moduleRegistry.append('GameTopLeft', TransitButton);
    moduleRegistry.append('Game', TransitPanel);

}) as ModRegistrar;