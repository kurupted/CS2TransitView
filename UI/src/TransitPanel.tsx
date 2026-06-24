import React, { useState, useEffect, memo, useRef } from 'react';
import { bindValue, trigger, useValue } from "cs2/api";
import { TransitType, SortField, TransitLine } from './types';
import { VanillaComponentResolver } from "./VanillaComponentResolver";

const showTransitPanel$ = bindValue<boolean>("BetterTransitView", "showTransitPanel", false);
const transitLinesData$ = bindValue<string>("BetterTransitView", "transitLinesData", "[]");
const showStopsAndStations$ = bindValue<boolean>("BetterTransitView", "showStopsAndStations", true);
const showInfoviewBackground$ = bindValue<boolean>("BetterTransitView", "showInfoviewBackground", false);
const showWaitingPassengers$ = bindValue<boolean>("BetterTransitView", "showWaitingPassengers", false);
const showTransitVehicles$ = bindValue<boolean>("BetterTransitView", "showTransitVehicles", false);
const selectedTransitLine$ = bindValue<number>("BetterTransitView", "selectedTransitLine", 0);
const isMapPickerActive$ = bindValue<boolean>("BetterTransitView", "isMapPickerActive", false);

const VehicleIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M4 16c0 .88.39 1.67 1 2.22V20c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h8v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1.78c.61-.55 1-1.34 1-2.22V6c0-3.5-3.58-4-8-4s-8 .5-8 4v10zm3.5 1c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm9 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm1.5-6H6V6h12v5z" /></svg>));
const DispatchIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '13rem', height: '13rem', marginLeft: '3rem' }} fill="none" stroke="#ffd700" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>));
const WarningIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '13rem', height: '13rem', marginLeft: '3rem' }} fill="none" stroke="#ff4d4d" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>));
const PassengerIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" /></svg>));
const LengthIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 7H3c-1.1 0-2 .9-2 2v6c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V9c0-1.1-.9-2-2-2zm0 8H3V9h2v3h2V9h2v3h2V9h2v3h2V9h2v6z" /></svg>));
const UsageIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6h-6z" /></svg>));
const CargoIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15 6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zM19 15.91v-6.7l-6 3.37v6.71l6-3.38z" /></svg>));
const StopIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z" /></svg>));


/*const OverlaySettingsIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '16rem', height: '16rem' }} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
        <circle cx="12" cy="12" r="3"></circle>
        <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
    </svg>
));*/

const MapMarkerIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '15rem', height: '15rem' }} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"></path>
        <circle cx="12" cy="10" r="3"></circle>
    </svg>
));

const PeopleIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '16rem', height: '16rem' }} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
        <circle cx="9" cy="7" r="4"></circle>
        <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
        <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
    </svg>
));

const BusIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '16rem', height: '16rem' }} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
        <path d="M6 17v4a2 2 0 0 0 2 2h0a2 2 0 0 0 2-2v-4"></path>
        <path d="M14 17v4a2 2 0 0 0 2 2h0a2 2 0 0 0 2-2v-4"></path>
        <path d="M8 9h8"></path>
    </svg>
));

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

const CrosshairIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '16rem', height: '16rem' }} fill="none" stroke="#bbb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="12" cy="12" r="10"></circle>
        <line x1="22" y1="12" x2="18" y2="12"></line>
        <line x1="6" y1="12" x2="2" y2="12"></line>
        <line x1="12" y1="6" x2="12" y2="2"></line>
        <line x1="12" y1="22" x2="12" y2="18"></line>
    </svg>
));

const TransportTypeIcon = memo(({ type }: { type: TransitType }) => {
    let path = "";
    switch (type) {
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
        <path d="M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z" />
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
const CustomDropdown = ({ value, options, onChange }: { value: string, options: { value: string, label: string }[], onChange: (val: string) => void }) => {
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
                    fontSize: '12rem',
                    width: '90rem',
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
    const theme = VanillaComponentResolver.instance.ToolOptionsTheme;
    const isVisible = useValue(showTransitPanel$);
    const rawData = useValue(transitLinesData$);
    const showStopsAndStations = useValue(showStopsAndStations$);
    const showWaitingPassengers = useValue(showWaitingPassengers$);
    const showTransitVehicles = useValue(showTransitVehicles$);
    const [isSettingsOpen, setIsSettingsOpen] = useState(false);
    const showInfoviewBackground = useValue(showInfoviewBackground$);

    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set());
    const knownLineIds = useRef<Set<number>>(new Set());
    const [isOverflowOpen, setIsOverflowOpen] = useState(false);
    const isPickerMode = useValue(isMapPickerActive$);
    const selectedTransitLine = useValue(selectedTransitLine$);

    // Sorting States
    const [sortField, setSortField] = useState<SortField>('name');
    const [sortDesc, setSortDesc] = useState<boolean>(false);

    const sortOptions: SortField[] = ['name', 'usage', 'vehicles', 'passengers', 'length', 'stops'];
    const sortLabels: Record<SortField, string> = {
        name: 'Name',
        usage: 'Usage %',
        vehicles: 'Vehicles',
        passengers: activeTab === 'cargo' ? 'Cargo' : 'Passengers',
        length: 'Distance',
        stops: 'Stops'
    };

    let lines: TransitLine[] = [];
    try { if (rawData && rawData !== "[]") lines = JSON.parse(rawData); } catch (e) { }

    // Keep track of new lines
    useEffect(() => {
        if (!isVisible) {
            // SAFETY CHECK: Only clear state if it's not already empty
            if (knownLineIds.current.size > 0) {
                knownLineIds.current.clear();
                setActiveLines(new Set());
            }
            return;
        }

        if (lines.length > 0) {
            setActiveLines(prev => {
                let isNewData = false;
                const nextActive = new Set(prev);

                lines.forEach(l => {
                    // If the UI has never seen this specific line ID before...
                    if (!knownLineIds.current.has(l.id)) {
                        isNewData = true;
                        knownLineIds.current.add(l.id);
                        if (l.visible) {
                            nextActive.add(l.id);
                        }
                    }
                });

                return isNewData ? nextActive : prev;
            });
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps 
    }, [isVisible, rawData]);

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

    useEffect(() => {
        if (selectedTransitLine !== 0 && lines.length > 0) {
            const line = lines.find(l => l.id === selectedTransitLine);
            if (line) {
                if (activeTab === 'cargo' && !line.cargo) {
                    setActiveTab(line.type === 'none' ? 'bus' : line.type);
                } else if (activeTab !== 'cargo' && line.cargo) {
                    setActiveTab('cargo');
                } else if (!line.cargo && line.type !== activeTab && line.type !== 'none') {
                    setActiveTab(line.type);
                }

                const targetLineId = selectedTransitLine;
                let attempts = 0;

                const tryScroll = () => {
                    const el = document.getElementById(`transit-line-${targetLineId}`);
                    if (el && el.clientHeight > 0) {
                        // 1. Scroll safely without using unsupported scrollIntoView()
                        const container = document.getElementById('btv-transit-list-container');
                        if (container) {
                            const targetScroll = el.offsetTop - (container.clientHeight / 2) + (el.clientHeight / 2);
                            container.scrollTop = targetScroll;
                        }

                        // 2. Flash background instantly
                        el.style.transition = 'none';
                        el.style.backgroundColor = 'rgba(66, 135, 245, 0.8)';

                        // 3. Fade out after a tiny delay so the browser registers the flash
                        setTimeout(() => {
                            el.style.transition = 'background-color 1.5s ease-out';
                            el.style.backgroundColor = 'rgba(255, 255, 255, 0.05)';
                        }, 50);

                        // 4. Safely reset the C# state AFTER the animation is totally finished (2 seconds)
                        setTimeout(() => {
                            trigger("BetterTransitView", "resetSelectedTransitLine");
                        }, 2000);

                    } else if (attempts < 20) {
                        attempts++;
                        setTimeout(tryScroll, 50);
                    } else {
                        trigger("BetterTransitView", "resetSelectedTransitLine");
                    }
                };

                tryScroll();
            }
        }
    }, [selectedTransitLine]);

    if (!isVisible) return null;

    const currentLines = lines.filter(l => {
        if (activeTab === 'cargo') return l.cargo;
        return !l.cargo && (l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
    });

    const sortedLines = [...lines].filter(l => {
        if (activeTab === 'cargo') return l.cargo;
        return !l.cargo && (l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
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

        // Secondary Tie-Breaker Sort (If values are identical, sort by ID)
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
    const panelOpacity = showInfoviewBackground ? 1.0 : 0.98;

    if (!isVisible) return null;

    return (
        <> {isPickerMode && (
            <div style={{
                position: 'absolute',
                top: '10vh',
                left: '50vw',
                transform: 'translateX(-50%)',
                backgroundColor: 'rgba(0, 0, 0, 0.85)',
                padding: '15rem 30rem',
                borderRadius: '10rem',
                color: 'white',
                fontSize: '22rem',
                fontWeight: 'bold',
                zIndex: 9999,
                pointerEvents: 'none',
                border: '2rem solid rgba(255, 255, 255, 0.2)',
                boxShadow: '0 4rem 20rem rgba(0, 0, 0, 0.6)'
            }}>
                Click a transit line on the map
            </div>
        )}
            <div style={{
                position: 'absolute',
                top: '60rem',
                left: '10rem',
                pointerEvents: 'none'
            }}>
                <div className={theme?.toolOptionsPanel}
                    style={{
                        width: '450rem', maxHeight: '800rem', padding: '12rem', pointerEvents: 'auto', display: 'flex', flexDirection: 'column',
                        opacity: panelOpacity,
                        backgroundImage: 'none',
                        backgroundColor: `rgba(42, 55, 83, ${panelOpacity})`,
                        backdropFilter: theme?.toolOptionsPanel ? undefined : 'blur(10px)',
                        border: '1rem solid rgba(255, 255, 255, 0.1)',
                        borderRadius: theme?.toolOptionsPanel ? undefined : '6rem'
                    }}>

                    <div style={{ padding: '10rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>Transit View</h2>
                        <div style={{ display: 'flex', alignItems: 'center' }} id="divtopToggles">
                            {/* Gray Map Toggle */}
                            <div onClick={() => trigger("BetterTransitView", "setShowInfoviewBackground", !showInfoviewBackground)} style={{ display: 'flex', alignItems: 'center', fontSize: '11rem', cursor: 'pointer', color: showInfoviewBackground ? '#fff' : '#aaa', backgroundColor: showInfoviewBackground ? '#4287f5' : 'rgba(255,255,255,0.1)', padding: '4rem 8rem', borderRadius: '4rem', transition: 'all 0.2s', fontWeight: showInfoviewBackground ? 'bold' : 'normal' }} title="Toggle Gray Map">
                                Map
                            </div>

                            {/* Separator */}
                            <div style={{ width: '1px', height: '16rem', backgroundColor: 'rgba(255,255,255,0.1)', marginLeft: '1rem' }} />

                            {/* Stops Toggle */}
                            <div onClick={() => trigger("BetterTransitView", "setShowStopsAndStations", !showStopsAndStations)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', color: showStopsAndStations ? '#fff' : '#aaa', backgroundColor: showStopsAndStations ? '#4287f5' : 'rgba(255,255,255,0.1)', padding: '4rem', marginLeft: '1rem', borderRadius: '4rem', transition: 'all 0.2s' }} title="Show Stops">
                                <MapMarkerIcon />
                            </div>

                            {/* Passengers Toggle */}
                            <div onClick={() => { if (showStopsAndStations) trigger("BetterTransitView", "setShowWaitingPassengers", !showWaitingPassengers) }} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: showStopsAndStations ? 'pointer' : 'not-allowed', color: showWaitingPassengers ? '#fff' : '#aaa', backgroundColor: showWaitingPassengers ? '#4287f5' : 'rgba(255,255,255,0.1)', opacity: showStopsAndStations ? 1 : 0.3, padding: '4rem', marginLeft: '1rem', borderRadius: '4rem', transition: 'all 0.2s' }} title="Show Waiting Passengers">
                                <PeopleIcon />
                            </div>

                            {/* Vehicles Toggle */}
                            <div onClick={() => trigger("BetterTransitView", "setShowTransitVehicles", !showTransitVehicles)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', color: showTransitVehicles ? '#fff' : '#aaa', backgroundColor: showTransitVehicles ? '#4287f5' : 'rgba(255,255,255,0.1)', padding: '4rem', marginLeft: '1rem', borderRadius: '4rem', transition: 'all 0.2s' }} title="Show Vehicles">
                                <BusIcon />
                            </div>

                            {/* Toggle All Button */}
                            <button onClick={toggleMasterAll} style={{ backgroundColor: 'rgba(255,255,255,0.15)', border: '1rem solid rgba(255,255,255,0.3)', color: 'white', padding: '4rem 8rem', borderRadius: '4rem', cursor: 'pointer', fontSize: '11rem', textTransform: 'uppercase', marginLeft: '25rem' }}>
                                Toggle All
                            </button>

                            <button onClick={() => trigger("BetterTransitView", "toggleTransitCustom", false)} style={{ backgroundColor: ' rgba(0,0,0,0.5)', border: 'none', cursor: 'pointer', marginLeft: '15rem', padding: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <CloseIcon />
                            </button>
                        </div>
                    </div>

                    <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)', position: 'relative' }}>
                        {['bus', 'train', 'subway', 'tram', 'ferry', 'cargo'].map((tab) => (
                            <button key={tab} onClick={() => { setActiveTab(tab as TransitType); setIsOverflowOpen(false); }} style={{ flex: 1, padding: '10rem 0', cursor: 'pointer', fontSize: '13rem', background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent', border: 'none', color: activeTab === tab ? 'white' : '#888', borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent' }}>
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
                        <div style={{ display: 'flex', alignItems: 'center' }}>

                            <div style={{ display: 'flex', alignItems: 'center', fontSize: '12rem', color: '#888' }}>
                                Sort: &nbsp;
                                <CustomDropdown
                                    value={sortField}
                                    options={sortOptions.map(opt => ({ value: opt, label: sortLabels[opt] }))}
                                    onChange={(val) => setSortField(val as SortField)}
                                />
                                <button onClick={() => setSortDesc(!sortDesc)} style={{ background: 'rgba(255,255,255,0.05)', border: '1rem solid rgba(255,255,255,0.1)', borderRadius: '4rem', color: '#fff', cursor: 'pointer', padding: '4rem 8rem', marginLeft: '1rem', fontSize: '12rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                    {sortDesc ? 'DESC ↓' : 'ASC ↑'}
                                </button>
                            </div>

                            {/* TOOL BUTTON */}
                            <button
                                onClick={() => trigger("BetterTransitView", "activateTransitTool", activeTab)}
                                style={{ marginLeft: '15rem', backgroundColor: '#4287f5', border: 'none', borderRadius: '4rem', color: 'white', padding: '4rem 10rem', cursor: 'pointer', display: 'flex', alignItems: 'center', fontSize: '12rem', fontWeight: 'bold' }}
                                title={`Equip ${activeTab} tool`}
                            >
                                <ToolIcon /> &nbsp;Tool
                            </button>

                            {/* PICKER BUTTON */}
                            <button
                                onClick={() => {
                                    trigger("BetterTransitView", "toggleMapPicker", !isPickerMode);
                                }}
                                style={{ marginLeft: '3rem', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem 8rem', borderRadius: '4rem', cursor: 'pointer', backgroundColor: isPickerMode ? 'rgba(255, 0, 0, 0.5)' : 'rgba(255,255,255,0.05)', color: isPickerMode ? 'white' : '#aaa', border: '1rem solid rgba(255,255,255,0.1)' }}
                                title="Pick a line on the map"
                            >
                                <CrosshairIcon />
                            </button>

                        </div>
                        <div onClick={toggleTabAll} style={{ display: 'flex', alignItems: 'center', fontSize: '13rem', cursor: 'pointer', color: '#fff' }}>
                            Toggle Tab <CustomCheckbox checked={allVisibleInTab} onChange={() => { }} />
                        </div>
                    </div>

                    <div id="btv-transit-list-container" style={{ padding: '10rem', overflowY: 'auto', flex: 1, position: 'relative' }}>
                        {sortedLines.length === 0 ? (
                            <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                        ) : sortedLines.map(line => (
                            <div id={`transit-line-${line.id}`} key={line.id} onClick={() => toggleLine(line.id)} style={{ display: 'flex', alignItems: 'center', padding: '10rem', marginBottom: '8rem', backgroundColor: 'rgba(255,255,255,0.05)', borderRadius: '6rem', borderLeft: `4rem solid ${line.color}`, cursor: 'pointer' }}>

                                {/* Type Icon is dynamically added in the Cargo Tab */}
                                {activeTab === 'cargo' && (
                                    <div style={{ marginRight: '10rem', display: 'flex', alignItems: 'center' }} title={`Type: ${line.type}`}>
                                        <TransportTypeIcon type={line.type} />
                                    </div>
                                )}

                                <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
                                    <div style={{ fontWeight: 'bold', fontSize: '16rem', marginBottom: '8rem', display: 'flex', alignItems: 'center' }}>
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

                                        <span style={{ display: 'flex', alignItems: 'center', width: '80rem' }} title="Length">
                                            <LengthIcon /> {line.length}
                                        </span>
                                    
                                        <span style={{ display: 'flex', alignItems: 'center', width: '60rem' }} title="Stops">
                                            <StopIcon /> {line.stops || 0}
                                        </span>

                                        <span style={{ display: 'flex', alignItems: 'center', width: '65rem',
                                            color: line.hasShortage ? '#ff4d4d' : (line.isDispatching ? '#ffd700' : '#bbb'),
                                            fontWeight: line.hasShortage || line.isDispatching ? 'bold' : 'normal'
                                            }} title={line.hasShortage ? "Not enough vehicles available from the depot" : (line.isDispatching ? "Vehicle(s) on the way from the depot" : "")}
                                        >
                                            <VehicleIcon /> <span style={{ marginLeft: '2rem' }}>{line.vehicles}</span>

                                            {line.hasShortage ? <span style={{ marginLeft: '2rem' }}><WarningIcon /></span> : (line.isDispatching ? <span style={{ marginLeft: '3rem' }}><DispatchIcon /></span> : null)}
                                        </span>
                                    
                                        {line.cargo ? (
                                            <span style={{ display: 'flex', alignItems: 'center', width: '75rem' }} title="Cargo Transported">
                                                <CargoIcon /> {((line.passengers || 0) / 1000).toFixed(0)} t
                                            </span>
                                        ) : (
                                            <span style={{ display: 'flex', alignItems: 'center', width: '75rem' }} title="Passengers">
                                                <PassengerIcon /> {line.passengers || 0}
                                            </span>
                                        )}
                                    
                                        <span style={{ display: 'flex', alignItems: 'center', width: '60rem' }} title="Usage">
                                            <UsageIcon /> {line.usage}%
                                        </span>
                                    </div>
                                </div>

                                {/* Dummy onChange protects bubbling conflicts but relies on row's click trigger natively */}
                                <div style={{ marginLeft: '15rem', flexShrink: 0 }}>
                                    <CustomCheckbox checked={activeLines.has(line.id)} onChange={() => { }} />
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </>
    );
};