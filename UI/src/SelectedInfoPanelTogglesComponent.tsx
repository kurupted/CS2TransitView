import { getModule } from "cs2/modding";
import { VanillaComponentResolver } from "./VanillaComponentResolver";
import {
    activityData, setTrafficFilter,
    highlightAgents, sethighlightAgents,
    displayMode, setDisplayMode,
    directionMode, setDirectionMode,
    showRoutes, setShowRoutes,
    rangeMode, setRangeMode,
    associatedStops, selectStop,
    walkingOnly, setWalkingOnly,
    isTransitStopSelected,
    hasParent, selectParent
} from "./bindings";
import { useValue } from "cs2/api";
import { useMemo, useState } from "react";
import { SegmentActivity } from "./types";

interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

interface StopOption {
    index: number;
    version: number;
    name: string;
}

const InfoSectionTheme: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss",
    "classes"
);

const InfoRowTheme: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
);

const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
);

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
);

export const SelectedInfoPanelTogglesComponent = (componentList: any): any => {

    const renderButtonRow = (label: string, buttons: { label: string, active: boolean, onClick: () => void }[]) => {
        return (
            <div style={{ display: 'flex', flexDirection: 'column', marginBottom: '5px', width: '100%' }}>
                <div style={{ color: "rgba(255,255,255,0.8)", fontSize: "0.9em", marginBottom: '2px', marginLeft: '4px' }}>{label}</div>
                <div style={{ display: 'flex', flexDirection: 'row', flexWrap: 'wrap', gap: '4px', width: '100%' }}>
                    {buttons.map((btn, idx) => (
                        <div
                            key={idx}
                            onClick={btn.onClick}
                            style={{
                                flex: "1 0 auto",
                                backgroundColor: btn.active ? "rgba(100, 255, 100, 0.8)" : "rgba(0, 0, 0, 0.4)",
                                color: btn.active ? "black" : "white",
                                border: "1px solid rgba(255,255,255,0.3)",
                                borderRadius: "3px",
                                textAlign: "center",
                                padding: "2px 6px",
                                fontSize: "0.85em",
                                cursor: "pointer",
                                userSelect: "none",
                                minWidth: "40px"
                            }}
                        >
                            {btn.label}
                        </div>
                    ))}
                </div>
            </div>
        );
    };

    const renderRow = (label: string, count: number, filterKey: string, activeFilter: string, setActive: any) => {
        if (!count || count <= 0) return null;
        const isSelected = activeFilter === filterKey;
        const displayLabel = isSelected ? `> ${label}` : label;
        const textStyle: React.CSSProperties = {
            color: isSelected ? 'rgba(255, 235, 100, 1)' : 'rgba(120, 200, 255, 1)',
            fontWeight: isSelected ? '800' : 'normal',
        };

        return (
            <div key={label} onClick={() => { const newValue = isSelected ? "" : filterKey; setActive(newValue); setTrafficFilter(filterKey); }} style={{ cursor: "pointer" }}>
                <InfoRow
                    left={<span style={textStyle}>{displayLabel}</span>}
                    right={<span style={textStyle}>{count.toString()}</span>}
                    uppercase={false} disableFocus={true} subRow={false} className={InfoRowTheme?.infoRow}
                />
            </div>
        );
    };

    componentList["BetterTransitView.Systems.TrafficUISystem"] = (e: InfoSectionComponent) => {
        const jsonString = useValue(activityData);
        const showHighlights = useValue(highlightAgents);
        const currentDisplayMode = useValue(displayMode) || 0;
        const showPathLines = useValue(showRoutes);
        const currentDirMode = useValue(directionMode) || 0;
        const currentRangeMode = useValue(rangeMode) ?? 1;
        const currentWalkingOnly = useValue(walkingOnly) ?? true;
        const isTransitStop = useValue(isTransitStopSelected) ?? false;
        const currentHasParent = useValue(hasParent) ?? false;

        const [activeFilter, setActiveFilter] = useState<string>("");

        const associatedStopsJson = useValue(associatedStops);
        const stops: StopOption[] = useMemo(() => {
            try { return JSON.parse(associatedStopsJson || "[]"); } catch(e) { return []; }
        }, [associatedStopsJson]);

        const data: SegmentActivity = useMemo(() => {
            try { return JSON.parse(jsonString || "{}"); } catch(e) { return {}; }
        }, [jsonString]);

        if (!data || Object.keys(data).length === 0) return null;

        const total = (data.none || 0) + (data.shopping || 0) + (data.leisure || 0) +
            (data.goingHome || 0) + (data.goingToWork || 0) + (data.movingIn || 0) + (data.movingAway || 0) +
            (data.school || 0) + (data.transporting || 0) + (data.returning || 0) +
            (data.tourism || 0) + (data.other || 0) + (data.services || 0);

        const totalStyle: React.CSSProperties = {
            color: activeFilter === "" ? 'rgba(255, 235, 100, 1)' : 'rgba(120, 200, 255, 1)',
            fontWeight: activeFilter === "" ? '800' : 'normal',
        };

        const sortedRows = [
            { label: "Going Home", count: data.goingHome || 0, key: "goingHome" },
            { label: "Going to Work", count: data.goingToWork || 0, key: "goingToWork" },
            { label: "Going to School", count: data.school || 0, key: "school" },
            { label: "Shopping", count: data.shopping || 0, key: "shopping" },
            { label: "Leisure", count: data.leisure || 0, key: "leisure" },
            { label: "Transporting / Delivery", count: data.transporting || 0, key: "transporting" },
            { label: "Returning Truck", count: data.returning || 0, key: "returning" },
            { label: "Services", count: data.services || 0, key: "services" },
            { label: "Tourism", count: data.tourism || 0, key: "tourism" },
            { label: "Moving In", count: data.movingIn || 0, key: "movingIn" },
            { label: "Moving Away", count: data.movingAway || 0, key: "movingAway" },
            { label: "Other", count: data.other || 0, key: "other" },
        ].sort((a, b) => b.count - a.count);

        return (
            <InfoSection
                focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                disableFocus={true}
                className={InfoSectionTheme?.infoSection}
            >
                {/* 0. PARENT BUTTON */}
                {isTransitStop && currentHasParent && renderButtonRow("Navigation", [
                    { label: "< Back to Parent Station / Road", active: false, onClick: () => selectParent() }
                ])}

                {/* 1. STOPS & PLATFORMS */}
                {stops.length > 0 && renderButtonRow("Select Stop / Platform", [
                    ...stops.map(stop => ({
                        label: stop.name,
                        active: false,
                        onClick: () => selectStop({ index: stop.index, version: stop.version })
                    })),
                    ...(!isTransitStop && currentDisplayMode === 1 ? [{
                        label: "Walking Only",
                        active: currentWalkingOnly,
                        onClick: () => setWalkingOnly(!currentWalkingOnly)
                    }] : [])
                ])}

                {/* 2. MODE TOGGLES (Vehicles vs Peds vs Transit) */}
                {isTransitStop ? (
                    renderButtonRow("Traffic Type", [
                        { label: "Transit Passengers", active: true, onClick: () => {} }
                    ])
                ) : (
                    renderButtonRow("Traffic Type", [
                        { label: "Vehicles", active: currentDisplayMode === 0, onClick: () => setDisplayMode(0) },
                        { label: "Pedestrians", active: currentDisplayMode === 1, onClick: () => setDisplayMode(1) }
                    ])
                )}

                {/* 3. DIRECTION TOGGLES */}
                {!isTransitStop && renderButtonRow("Road Side / Direction", [
                    { label: "Both", active: currentDirMode === 0, onClick: () => setDirectionMode(0) },
                    { label: "Side A", active: currentDirMode === 1, onClick: () => setDirectionMode(1) },
                    { label: "Side B", active: currentDirMode === 2, onClick: () => setDirectionMode(2) }
                ])}

                {/* 4. RANGE TOGGLES */}
                { !isTransitStop && renderButtonRow("Max Range", [
                    { label: "Lane Data Only", active: currentRangeMode === 0, onClick: () => setRangeMode(0) },
                    { label: "1km (0.6mi)", active: currentRangeMode === 1, onClick: () => setRangeMode(1) },
                    { label: "2km (1.2mi)", active: currentRangeMode === 2, onClick: () => setRangeMode(2) },
                    { label: "∞", active: currentRangeMode === 3, onClick: () => setRangeMode(3) }
                ])}

                {/* 5. HIGHLIGHTS & ROUTE TOGGLES */}
                <div style={{ display: 'flex', flexDirection: 'row', gap: '4px', width: '100%', marginBottom: '10px' }}>
                    <div style={{ flex: 1 }}>
                        {renderButtonRow("Highlights", [
                            { label: showHighlights ? "ON" : "OFF", active: showHighlights, onClick: () => sethighlightAgents(!showHighlights) }
                        ])}
                    </div>
                    <div style={{ flex: 1 }}>
                        {renderButtonRow("Route Lines", [
                            { label: showPathLines ? "ON" : "OFF", active: showPathLines, onClick: () => setShowRoutes(!showPathLines) }
                        ])}
                    </div>
                </div>

                {/* 6. RESET FILTER / TOTAL ROW */}
                <div onClick={() => { setActiveFilter(""); setTrafficFilter("RESET"); }} style={{ cursor: "pointer", marginBottom: "5px", borderTop: "1px solid rgba(255,255,255,0.1)", paddingTop: "5px" }}>
                    <InfoRow
                        left={<span style={totalStyle}>{activeFilter === "" ? "> ALL ACTIVITY" : "RESET FILTER"}</span>}
                        right={<span style={totalStyle}>{total.toString()}</span>}
                        uppercase={true} disableFocus={true} subRow={false} className={InfoRowTheme?.infoRow}
                    />
                </div>

                {/* 7. DATA ROWS */}
                {sortedRows.map((row) => row.count > 0 ? renderRow(row.label, row.count, row.key, activeFilter, setActiveFilter) : null)}
                {data.none > 0 ? renderRow("None / Unknown", data.none, "none", activeFilter, setActiveFilter) : null}

            </InfoSection>
        );
    };
    return componentList;
}