import { bindValue, trigger } from "cs2/api";

export const toolActive = bindValue<boolean>("BetterTransitView", "toolActive", false);
export const activityData = bindValue<string>("BetterTransitView", "activityData", "{}");
export const highlightAgents = bindValue<boolean>("BetterTransitView", "highlightAgents", false);
export const displayMode = bindValue<number>("BetterTransitView", "displayMode", 0);
export const showRoutes = bindValue<boolean>("BetterTransitView", "showRoutes", false);
export const directionMode = bindValue<number>("BetterTransitView", "directionMode", 0);
export const rangeMode = bindValue<number>("BetterTransitView", "rangeMode", 1);

export const setTrafficFilter = (filter: string) => trigger("BetterTransitView", "setTrafficFilter", filter);
export const sethighlightAgents = (active: boolean) => trigger("BetterTransitView", "sethighlightAgents", active);
export const setDisplayMode = (mode: number) => trigger("BetterTransitView", "setDisplayMode", mode);
export const setShowRoutes = (active: boolean) => trigger("BetterTransitView", "setShowRoutes", active);

export const setDirectionMode = (mode: number) => trigger("BetterTransitView", "setDirectionMode", mode);
export const setRangeMode = (mode: number) => trigger("BetterTransitView", "setRangeMode", mode);

export const associatedStops = bindValue<string>("BetterTransitView", "associatedStops", "[]");
export const selectStop = (entity: { index: number; version: number }) => trigger("BetterTransitView", "selectStop", entity as any);

export const walkingOnly = bindValue<boolean>("BetterTransitView", "walkingOnly", true);
export const setWalkingOnly = (active: boolean) => trigger("BetterTransitView", "setWalkingOnly", active);

export const isTransitStopSelected = bindValue<boolean>("BetterTransitView", "isTransitStopSelected", false);

export const hasParent = bindValue<boolean>("BetterTransitView", "hasParent", false);
export const selectParent = () => trigger("BetterTransitView", "selectParent");

export const transitOpen = bindValue<boolean>("BetterTransitView", "showTransitPanel", false);