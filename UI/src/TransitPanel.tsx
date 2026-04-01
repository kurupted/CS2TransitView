import React, { useState, useEffect, memo } from 'react';
import { bindValue, trigger, useValue } from "cs2/api";
import { TransitType, SortField, TransitLine } from './types';

const showTransitPanel$ = bindValue<boolean>("BetterTransitView", "showTransitPanel", false);
const transitLinesData$ = bindValue<string>("BetterTransitView", "transitLinesData", "[]");
const showStopsAndStations$ = bindValue<boolean>("BetterTransitView", "showStopsAndStations", true);
const showInfoviewBackground$ = bindValue<boolean>("BetterTransitView", "showInfoviewBackground", true);

const VehicleIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M4 16c0 .88.39 1.67 1 2.22V20c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h8v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1.78c.61-.55 1-1.34 1-2.22V6c0-3.5-3.58-4-8-4s-8 .5-8 4v10zm3.5 1c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm9 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm1.5-6H6V6h12v5z"/></svg>));
const PassengerIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>));
const LengthIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 7H3c-1.1 0-2 .9-2 2v6c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V9c0-1.1-.9-2-2-2zm0 8H3V9h2v3h2V9h2v3h2V9h2v3h2V9h2v6z"/></svg>));
const UsageIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6h-6z"/></svg>));
const CargoIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15 6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zM19 15.91v-6.7l-6 3.37v6.71l6-3.38z"/></svg>));
const StopIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/></svg>));

const ToolIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 20h9" />
        <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
    </svg>
));

const SearchIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="none" stroke="#bbb" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="11" cy="11" r="8"></circle>
        <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
    </svg>
));

const TransportTypeIcon = memo(({ type }: { type: TransitType }) => {
    let path = "";
    switch(type) {
        case 'train':
        case 'subway':
            path = "M12 2c-4 0-8 .5-8 4v9.5C4 17.43 5.57 19 7.5 19L6 20.5v.5h12v-.5L16.5 19c1.93 0 3.5-1.57 3.5-3.5V6c0-3.5-4-4-8-4zM7.5 17c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm3.5-7H6V6h5v4zm4 0h-2V6h2v4zm2.5 7c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z";
            break;
        case 'ship':
        case 'ferry':
            path = "M20 21c-1.39 0-2.78-.47-4-1.32-2.44 1.71-5.56 1.71-8 0C6.78 20.53 5.39 21 4 21H2v2h2c1.38 0 2.74-.35 4-.99 2.52 1.29 5.48 1.29 8 0 1.26.65 2.62.99 4 .99h2v-2h-2zM3.95 19H4c1.6 0 3.02-.88 4-2 .98 1.12 2.4 2 4 2s3.02-.88 4-2c.98 1.12 2.4 2 4 2h.05l1.89-6.68c.08-.26.06-.54-.06-.78s-.34-.42-.6-.5L20 10.62V6c0-1.1-.9-2-2-2h-3V1H9v3H6c-1.1 0-2 .9-2 2v4.62l-1.29.42c-.26.08-.48.26-.6.5s-.15.52-.06.78L3.95 19zM6 6h12v3.97L12 8 6 9.97V6z";
            break;
        case 'airplane':
            path = "M21 16v-2l-8-5V3.5c0-.83-.67-1.5-1.5-1.5S10 2.67 10 3.5V9l-8 5v2l8-2.5V19l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-5.5l8 2.5z";
            break;
        case 'bus':
        case 'tram':
            path = "M4 16c0 .88.39 1.67 1 2.22V20c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h8v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1.78c.61-.55 1-1.34 1-2.22V6c0-3.5-3.58-4-8-4s-8 .5-8 4v10zm3.5 1c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm9 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm1.5-6H6V6h12v5z";
            break;
        default:
            path = "M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15 6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zM19 15.91v-6.7l-6 3.37v6.71l6-3.38z"; // Cargo Box
    }
    return (
        <svg viewBox="0 0 24 24" style={{ width: '18rem', height: '18rem' }} fill="#bbb">
            <path d={path} />
        </svg>
    );
});

const MoreIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '18rem', height: '18rem' }} fill="#bbb">
        <path d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z"/>
    </svg>
));

const CloseIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '18rem', height: '18rem' }} fill="none" stroke="#aaa" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <line x1="18" y1="6" x2="6" y2="18"></line>
        <line x1="6" y1="6" x2="18" y2="18"></line>
    </svg>
));

const CustomCheckbox = ({ checked, onChange }: { checked: boolean, onChange: () => void }) => (
    <div onClick={onChange} style={{ width: '18rem', height: '18rem', border: '1rem solid rgba(255,255,255,0.3)', borderRadius: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', backgroundColor: checked ? '#4287f5' : 'rgba(0,0,0,0.5)', flexShrink: 0 }}>
        {checked && <span style={{ color: 'white', fontSize: '14rem', lineHeight: '18rem' }}>✓</span>}
    </div>
);

// Custom, Crash-Proof React Dropdown
const CustomDropdown = ({ value, options, onChange }: { value: string, options: {value: string, label: string}[], onChange: (val: string) => void }) => {
    const [isOpen, setIsOpen] = useState(false);

    return (
        <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
            <button
                onClick={() => setIsOpen(!isOpen)}
                style={{
                    background: 'rgba(255,255,255,0.1)',
                    color: '#fff',
                    border: '1rem solid rgba(255,255,255,0.2)',
                    borderRadius: '4rem',
                    padding: '4rem 8rem',
                    cursor: 'pointer',
                    outline: 'none',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6rem',
                    fontSize: '12rem',
                    minWidth: '80rem',
                    justifyContent: 'space-between'
                }}
            >
                {options.find(o => o.value === value)?.label || "Select..."}
                <span style={{ fontSize: '8rem', opacity: 0.7 }}>▼</span>
            </button>

            {isOpen && (
                <>
                    <div onClick={() => setIsOpen(false)} style={{ position: 'fixed', inset: 0, zIndex: 999 }} />
                    <div style={{
                        position: 'absolute',
                        top: '100%',
                        right: 0,
                        marginTop: '4rem',
                        backgroundColor: 'rgba(25, 30, 35, 0.98)',
                        border: '1rem solid rgba(255,255,255,0.2)',
                        borderRadius: '4rem',
                        boxShadow: '0 4px 12px rgba(0,0,0,0.5)',
                        zIndex: 1000,
                        minWidth: '130rem',
                        overflow: 'hidden',
                        display: 'flex',
                        flexDirection: 'column'
                    }}>
                        {options.map(opt => (
                            <div
                                key={opt.value}
                                onClick={() => { onChange(opt.value); setIsOpen(false); }}
                                style={{
                                    padding: '6rem 10rem',
                                    cursor: 'pointer',
                                    fontSize: '12rem',
                                    color: opt.value === value ? '#4287f5' : '#ccc',
                                    backgroundColor: opt.value === value ? 'rgba(255,255,255,0.08)' : 'transparent',
                                    borderBottom: '1rem solid rgba(255,255,255,0.05)',
                                    transition: 'background-color 0.1s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.15)'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = opt.value === value ? 'rgba(255,255,255,0.08)' : 'transparent'}
                            >
                                {opt.label}
                            </div>
                        ))}
                    </div>
                </>
            )}
        </div>
    );
};

export const TransitPanel = () => {
    const isVisible = useValue(showTransitPanel$);
    const rawData = useValue(transitLinesData$);
    const showStopsAndStations = useValue(showStopsAndStations$);
    const showInfoviewBackground = useValue(showInfoviewBackground$);

    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set());
    const [hasInitialized, setHasInitialized] = useState(false);
    const [isOverflowOpen, setIsOverflowOpen] = useState(false);
    
    // Sorting States
    const [sortField, setSortField] = useState<SortField>('name');
    const [sortDesc, setSortDesc] = useState<boolean>(false);

    const sortOptions: SortField[] = ['name', 'usage', 'vehicles', 'passengers', 'length', 'stops'];
    const sortLabels: Record<SortField, string> = {
        name: 'Name',
        usage: 'Usage %',
        vehicles: 'Vehicles',
        passengers: 'Passengers/Cargo',
        length: 'Distance',
        stops: 'Stops'
    };

    let lines: TransitLine[] = [];
    try { if (rawData && rawData !== "[]") lines = JSON.parse(rawData); } catch (e) {}

    useEffect(() => {
        if (isVisible && lines.length > 0 && !hasInitialized) {
            setActiveLines(new Set(lines.filter(l => l.visible).map(l => l.id)));
            setHasInitialized(true);
        }

        // Reset initialization when the panel closes so it refreshes next time
        if (!isVisible && hasInitialized) {
            setHasInitialized(false);
            setActiveLines(new Set());
        }
    }, [isVisible, lines, hasInitialized]);

    // ... existing useEffect ...
    useEffect(() => {
        if (isVisible && lines.length > 0 && !hasInitialized) {
            setActiveLines(new Set(lines.filter(l => l.visible).map(l => l.id)));
            setHasInitialized(true);
        }

        if (!isVisible && hasInitialized) {
            setHasInitialized(false);
            setActiveLines(new Set());
        }
    }, [isVisible, lines, hasInitialized]);

    // Push vanilla Info Panel to the right when this UI is open
    useEffect(() => {
        if (!isVisible) return;

        const styleId = 'bettertransitview-vanilla-shifter';
        let styleEl = document.getElementById(styleId) as HTMLStyleElement;

        if (!styleEl) {
            styleEl = document.createElement('style');
            styleEl.id = styleId;
            // Target all selected-info-panels, but explicitly cancel the transform 
            // on nested ones so they don't double-jump!
            styleEl.innerHTML = `
                div[class*="selected-info-panel_"] {
                    transform: translateX(460rem) !important;
                    transition: transform 0.2s cubic-bezier(0.25, 0.1, 0.25, 1) !important;
                }
                
                div[class*="selected-info-panel_"] div[class*="selected-info-panel_"] {
                    transform: none !important;
                }
            `;
            document.head.appendChild(styleEl);
        }

        return () => {
            if (styleEl && styleEl.parentNode) {
                styleEl.parentNode.removeChild(styleEl);
            }
        };
    }, [isVisible]);

    if (!isVisible) return null;
    if (lines.length === 0) return (<div style={{ position: 'absolute', left: '60rem', top: '60rem', width: '320rem', backgroundColor: 'rgba(25, 30, 35, 0.95)', padding: '20rem', color: 'white' }}>Loading Transit Data...</div>);

    const currentLines = lines.filter(l => {
        if (activeTab === 'cargo') return l.cargo;
        return !l.cargo && (l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
    });
    
    const sortedLines = [...lines].filter(l => {
        if (activeTab === 'cargo') return l.cargo;
        return l.type === activeTab && !l.cargo;
    }).sort((a, b) => {
        let valA = a[sortField];
        let valB = b[sortField];

        // Ensure length uses a numeric comparison if available
        if (sortField === 'length') {
            valA = a.lengthRaw || parseFloat(a.length as string) || 0;
            valB = b.lengthRaw || parseFloat(b.length as string) || 0;
        }

        let comparison = 0;
        if (typeof valA === 'string' && typeof valB === 'string') {
            comparison = valA.localeCompare(valB);
        } else {
            comparison = (valA as number) > (valB as number) ? 1 : ((valA as number) < (valB as number) ? -1 : 0);
        }

        // Apply ASC / DESC
        if (sortDesc) comparison = -comparison;

        // NEW: Secondary Tie-Breaker Sort (If values are identical, sort by ID)
        if (comparison === 0) {
            comparison = a.id - b.id; // We keep this always ascending so jumping never occurs
        }

        return comparison;
    });

    const allVisibleInTab = sortedLines.length > 0 && sortedLines.every(l => activeLines.has(l.id));

    const toggleLine = (id: number) => {
        const next = new Set(activeLines);
        let willShow = false;
        if (next.has(id)) next.delete(id); else { next.add(id); willShow = true; }
        setActiveLines(next);
        trigger("BetterTransitView", "setLineVisible", id, willShow);
    };

    const toggleTabAll = () => {
        const next = new Set(activeLines);
        const targetState = !allVisibleInTab;
        sortedLines.forEach(l => {
            if (targetState) next.add(l.id); else next.delete(l.id);
            trigger("BetterTransitView", "setLineVisible", l.id, targetState);
        });
        setActiveLines(next);
    };

    const toggleMasterAll = () => {
        // If ANY line is off, the master toggle turns them all ON. Else, turns all OFF.
        const targetState = lines.some(l => !activeLines.has(l.id));

        const next = new Set<number>();
        if (targetState) {
            lines.forEach(l => next.add(l.id));
        }
        setActiveLines(next);

        // Tell the C# backend to update the visibility
        trigger("BetterTransitView", "setAllLinesVisible", targetState);
    };

    return (
        <div style={{ position: 'absolute', top: '55rem', left: '10rem', width: '450rem', maxHeight: '800rem', backgroundColor: 'var(--panelColorNormal)', borderRadius: '4rem', padding: '12rem', color: 'white', pointerEvents: 'auto', boxShadow: '0 4px 8px rgba(0,0,0,0.3)', display: 'flex', flexDirection: 'column' }}>

            <div style={{ padding: '10rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>Transit Overview</h2>
                <div style={{ display: 'flex', alignItems: 'center', gap: '15rem' }}>
                    <div onClick={() => trigger("BetterTransitView", "setShowInfoviewBackground", !showInfoviewBackground)} style={{ display: 'flex', alignItems: 'center', gap: '6rem', fontSize: '12rem', cursor: 'pointer', color: '#ccc' }}>
                        <CustomCheckbox checked={showInfoviewBackground} onChange={() => {}} />
                        &nbsp; Gray Map &nbsp;
                    </div>
                    <div onClick={() => trigger("BetterTransitView", "setShowStopsAndStations", !showStopsAndStations)} style={{ display: 'flex', alignItems: 'center', gap: '6rem', fontSize: '12rem', cursor: 'pointer', color: '#ccc' }}>
                        <CustomCheckbox checked={showStopsAndStations} onChange={() => {}} />
                        &nbsp; Stops &nbsp;
                    </div>
                    <button onClick={toggleMasterAll} style={{ backgroundColor: 'rgba(255,255,255,0.1)', border: '1rem solid rgba(255,255,255,0.2)', color: 'white', padding: '4rem 8rem', borderRadius: '4rem', cursor: 'pointer', fontSize: '11rem', textTransform: 'uppercase' }}>
                        Toggle All
                    </button>
                    <button onClick={() => trigger("BetterTransitView", "toggleTransitCustom", false)} style={{ backgroundColor: ' rgba(0,0,0,0.5)', border: 'none', cursor: 'pointer', marginLeft: '5rem', padding: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <CloseIcon />
                    </button>
                </div>
            </div>

            <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)', position: 'relative' }}>
                {['bus', 'train', 'subway', 'tram', 'ferry', 'cargo'].map((tab) => (
                    <button key={tab} onClick={() => setActiveTab(tab as TransitType)} style={{ flex: 1, padding: '10rem 0', cursor: 'pointer', fontSize: '13rem', background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent', border: 'none', color: activeTab === tab ? 'white' : '#888', borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent' }}>
                        {tab.charAt(0).toUpperCase() + tab.slice(1)}
                    </button>
                ))}

                {/* OVERFLOW BUTTON */}
                <button
                    onClick={() => setIsOverflowOpen(!isOverflowOpen)}
                    style={{
                        padding: '10rem 15rem', cursor: 'pointer', background: 'transparent', border: 'none',
                        borderBottom: (activeTab === 'airplane' || activeTab === 'ship') ? '2rem solid #4287f5' : '2rem solid transparent',
                        display: 'flex', alignItems: 'center', justifyContent: 'center'
                    }}
                >
                    <MoreIcon />
                </button>

                {/* DROPDOWN MENU */}
                {isOverflowOpen && (
                    <>
                        {/* Invisible click-away overlay */}
                        <div onClick={() => setIsOverflowOpen(false)} style={{ position: 'fixed', inset: 0, zIndex: 99 }} />

                        <div style={{
                            position: 'absolute', top: '100%', right: '0', backgroundColor: 'rgba(25, 30, 35, 0.98)',
                            border: '1rem solid rgba(255,255,255,0.2)', borderRadius: '4rem', boxShadow: '0 4px 12px rgba(0,0,0,0.5)',
                            zIndex: 100, display: 'flex', flexDirection: 'column', minWidth: '100rem'
                        }}>
                            {['airplane', 'ship'].map(tab => (
                                <div
                                    key={tab}
                                    onClick={() => { setActiveTab(tab as TransitType); setIsOverflowOpen(false); }}
                                    style={{
                                        padding: '10rem 15rem', cursor: 'pointer', fontSize: '13rem',
                                        color: activeTab === tab ? '#4287f5' : '#ccc',
                                        backgroundColor: activeTab === tab ? 'rgba(255,255,255,0.08)' : 'transparent',
                                        borderBottom: '1rem solid rgba(255,255,255,0.05)'
                                    }}
                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.15)'}
                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = activeTab === tab ? 'rgba(255,255,255,0.08)' : 'transparent'}
                                >
                                    {tab === 'airplane' ? 'Air' : 'Ship'}
                                </div>
                            ))}
                        </div>
                    </>
                )}
            </div>

            <div style={{ padding: '10rem 15rem', backgroundColor: 'rgba(0,0,0,0.2)', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8rem' }}>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '12rem', color: '#888' }}>
                        Sort: &nbsp;
                        <CustomDropdown
                            value={sortField}
                            options={sortOptions.map(opt => ({ value: opt, label: sortLabels[opt] }))}
                            onChange={(val) => setSortField(val as SortField)}
                        />
                        <button onClick={() => setSortDesc(!sortDesc)} style={{ background: 'rgba(255,255,255,0.05)', border: '1rem solid rgba(255,255,255,0.1)', borderRadius: '4rem', color: '#fff', cursor: 'pointer', padding: '4rem 8rem', fontSize: '12rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            {sortDesc ? 'DESC ↓' : 'ASC ↑'}
                        </button>
                    </div>

                    {/* NEW TOOL BUTTON */}
                    <button
                        onClick={() => trigger("BetterTransitView", "activateTransitTool", activeTab)}
                        style={{ marginLeft: '10rem', backgroundColor: '#4287f5', border: 'none', borderRadius: '4rem', color: 'white', padding: '4rem 10rem', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6rem', fontSize: '12rem', fontWeight: 'bold' }}
                        title={`Equip ${activeTab} tool`}
                    >
                        <ToolIcon /> &nbsp;Tool
                    </button>

                </div>
                {/* Fixed Toggle Tab label acting as clickable trigger */}
                <div onClick={toggleTabAll} style={{ display: 'flex', alignItems: 'center', gap: '8rem', fontSize: '13rem', cursor: 'pointer', color: '#fff' }}>
                    Toggle Tab <CustomCheckbox checked={allVisibleInTab} onChange={() => {}} />
                </div>
            </div>

            <div style={{ padding: '10rem', overflowY: 'auto', flex: 1 }}>
                {sortedLines.length === 0 ? (
                    <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                ) : sortedLines.map(line => (
                    <div key={line.id} onClick={() => toggleLine(line.id)} style={{ display: 'flex', alignItems: 'center', padding: '10rem', marginBottom: '8rem', backgroundColor: 'rgba(255,255,255,0.05)', borderRadius: '6rem', borderLeft: `4rem solid ${line.color}`, cursor: 'pointer' }}>

                {/* Type Icon is dynamically added in the Cargo Tab */}
                {activeTab === 'cargo' && (
                    <div style={{ marginRight: '10rem', display: 'flex', alignItems: 'center' }} title={`Type: ${line.type}`}>
                        <TransportTypeIcon type={line.type} />
                    </div>
                )}

                <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
                    <div style={{ fontWeight: 'bold', fontSize: '16rem', marginBottom: '8rem', display: 'flex', alignItems: 'center', gap: '8rem' }}>
                        <span style={{ whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                            {line.name} &nbsp;
                        </span>
                        <div
                            onClick={(e) => {
                                e.stopPropagation(); // Prevents the row from toggling visibility
                                trigger("BetterTransitView", "showVanillaLineInfo", line.id);
                            }}
                            title="Inspect Route"
                            style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem', borderRadius: '4rem', transition: 'background-color 0.1s', cursor: 'pointer', backgroundColor: 'rgba(255,255,255,0.05)' }}
                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.15)'}
                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.05)'}
                        >
                            <SearchIcon />
                        </div>
                    </div>

                    
                    <div style={{ fontSize: '14rem', color: '#bbb', display: 'flex', flexWrap: 'wrap', rowGap: '8rem' }}>
                        
                        <span style={{ display: 'flex', alignItems: 'center', gap: '4rem', width: '80rem' }} title="Length">
                            <LengthIcon /> {typeof line.length === 'string' ? line.length.replace(/([0-9.]+)([a-zA-Z]+)/g, '$1 $2') : line.length}
                        </span>

                        <span style={{ display: 'flex', alignItems: 'center', gap: '4rem', width: '60rem' }} title="Stops">
                            <StopIcon /> {line.stops || 0}
                        </span>
                        
                        <span style={{ display: 'flex', alignItems: 'center', gap: '4rem', width: '60rem' }} title="Vehicles">
                            <VehicleIcon /> {line.vehicles}
                        </span>

                        {line.cargo ? (
                            <span style={{ display: 'flex', alignItems: 'center', gap: '4rem', width: '80rem' }} title="Cargo Transported">
                                <CargoIcon /> {((line.passengers || 0) / 1000).toFixed(0)} t
                            </span>
                        ) : (
                            <span style={{ display: 'flex', alignItems: 'center', gap: '4rem', width: '80rem' }} title="Passengers">
                                <PassengerIcon /> {line.passengers || 0}
                            </span>
                        )}

                        <span style={{ display: 'flex', alignItems: 'center', gap: '4rem', width: '60rem' }} title="Usage">
                            <UsageIcon /> {line.usage}%
                        </span>
                    </div>
                </div>

                {/* Dummy onChange protects bubbling conflicts but relies on row's click trigger natively */}
                <div style={{ marginLeft: '15rem', flexShrink: 0 }}>
                    <CustomCheckbox checked={activeLines.has(line.id)} onChange={() => {}} />
                </div>
            </div>
            ))}
        </div>
</div>
);
};