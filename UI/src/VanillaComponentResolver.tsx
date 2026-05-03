import { FocusKey, UniqueFocusKey } from "cs2/bindings";
import { ModuleRegistry } from "cs2/modding";

const registryIndex = {
    FOCUS_DISABLED: ["game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED"],
    FOCUS_AUTO: ["game-ui/common/focus/focus-key.ts", "FOCUS_AUTO"],
    ToolOptionsTheme: ["game-ui/game/components/tool-options/tool-options-panel.module.scss", "classes"],
}

export class VanillaComponentResolver {
    public static get instance(): VanillaComponentResolver { return this._instance!! }
    private static _instance?: VanillaComponentResolver

    public static setRegistry(in_registry: ModuleRegistry) { this._instance = new VanillaComponentResolver(in_registry); }
    private registryData: ModuleRegistry;

    constructor(in_registry: ModuleRegistry) {
        this.registryData = in_registry;
    }

    private cachedData: Partial<Record<keyof typeof registryIndex, any>> = {}
    private updateCache(entry: keyof typeof registryIndex) {
        const entryData = registryIndex[entry];
        // Defensive check
        const module = this.registryData.registry.get(entryData[0]);
        return this.cachedData[entry] = module ? module[entryData[1]] : null;
    }

    public get FOCUS_DISABLED(): UniqueFocusKey { return this.cachedData["FOCUS_DISABLED"] ?? this.updateCache("FOCUS_DISABLED") }
    public get FOCUS_AUTO(): UniqueFocusKey { return this.cachedData["FOCUS_AUTO"] ?? this.updateCache("FOCUS_AUTO") }

    public get ToolOptionsTheme(): any { return this.cachedData["ToolOptionsTheme"] ?? this.updateCache("ToolOptionsTheme") }
}