import { bindValue, trigger } from "cs2/api";

export const hasParent = bindValue<boolean>("BetterTransitView", "hasParent", false);
export const selectParent = () => trigger("BetterTransitView", "selectParent");

export const transitOpen = bindValue<boolean>("BetterTransitView", "showTransitPanel", false);