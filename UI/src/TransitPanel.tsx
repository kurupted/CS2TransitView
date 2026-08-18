import React, { useState, useEffect, memo, useRef } from 'react';
import { bindValue, trigger, useValue } from "cs2/api";
import { Scrollable, Tooltip } from "cs2/ui";
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

const VehicleIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M20 7H4c-1.1 0-2 .9-2 2v7c0 .55.45 1 1 1h1.18c.41 1.16 1.52 2 2.82 2s2.41-.84 2.82-2h4.36c.41 1.16 1.52 2 2.82 2s2.41-.84 2.82-2H21c.55 0 1-.45 1-1v-6c0-1.66-1.34-3-3-3zM7 17.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm10 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zM4 12V9h4v3H4zm6 0V9h4v3h-4zm6 0V9h3.5c.83 0 1.5.67 1.5 1.5V12H16z" /></svg>));
const DispatchIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '13rem', height: '13rem', marginLeft: '3rem' }} fill="none" stroke="#ffd700" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>));
const WarningIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '13rem', height: '13rem', marginLeft: '3rem' }} fill="none" stroke="#ff4d4d" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>));
const PassengerIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" /></svg>));
const LengthIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 7H3c-1.1 0-2 .9-2 2v6c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V9c0-1.1-.9-2-2-2zm0 8H3V9h2v3h2V9h2v3h2V9h2v3h2V9h2v6z" /></svg>));
const UsageIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6h-6z" /></svg>));
const CargoIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15 6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zM19 15.91v-6.7l-6 3.37v6.71l6-3.38z" /></svg>));
const StopIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="#bbb"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z" /></svg>));
const WaitTimeIcon = memo(() => (<svg viewBox="0 0 24 24" style={{ width: '13rem', height: '13rem' }} fill="none" stroke="#bbb" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>));


/*const OverlaySettingsIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '16rem', height: '16rem' }} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
        <circle cx="12" cy="12" r="3"></circle>
        <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
    </svg>
));*/

const HourglassIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '15rem', height: '15rem' }} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <path d="M5 22h14"></path>
        <path d="M5 2h14"></path>
        <path d="M17 22v-4.172a2 2 0 0 0-.586-1.414L12 12l-4.414 4.414A2 2 0 0 0 7 17.828V22"></path>
        <path d="M7 2v4.172a2 2 0 0 0 .586 1.414L12 12l4.414-4.414A2 2 0 0 0 17 6.172V2"></path>
    </svg>
));

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
    <svg viewBox="0 0 24 24" style={{ width: '16rem', height: '16rem' }} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M20 7H4c-1.1 0-2 .9-2 2v7a1 1 0 0 0 1 1h1.2a3 3 0 0 0 5.6 0h4.4a3 3 0 0 0 5.6 0H21a1 1 0 0 0 1-1v-6c0-1.7-1.3-3-3-3z" />
        <circle cx="7" cy="17" r="1.5" />
        <circle cx="17" cy="17" r="1.5" />
        <path d="M4 12V9h4v3H4zm6 0V9h4v3h-4zm6 0V9h3.5a1.5 1.5 0 0 1 1.5 1.5V12H16z" />
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

const ChevronRightIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="none" stroke="#bbb" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="9 18 15 12 9 6"></polyline>
    </svg>
));

const ChevronDownIcon = memo(() => (
    <svg viewBox="0 0 24 24" style={{ width: '14rem', height: '14rem' }} fill="none" stroke="#bbb" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="6 9 12 15 18 9"></polyline>
    </svg>
));

const TransportTypeIcon = memo(({ type, style, noCircle }: { type: TransitType, style?: React.CSSProperties, noCircle?: boolean }) => {
    const svgFill = style?.fill || "#bbb";
    if (type === 'subway' && !noCircle) {
        return (
            <svg viewBox="0 0 24 24" style={{ width: '18rem', height: '18rem', ...style }} fill={svgFill}>
                <circle cx="12" cy="12" r="10" fill="none" stroke={svgFill} strokeWidth="1.8" />
                <path transform="translate(3.6, 3.6) scale(0.7)" d="M12 2c-4 0-8 .5-8 4v9.5C4 17.43 5.57 19 7.5 19L6 20.5v.5h12v-.5L16.5 19c1.93 0 3.5-1.57 3.5-3.5V6c0-3.5-4-4-8-4zM7.5 17c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm3.5-7H6V6h5v4zm4 0h-2V6h2v4zm2.5 7c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z" />
            </svg>
        );
    }
    let path = "";
    switch (type) {
        case 'train':
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
            path = "M20 7H4c-1.1 0-2 .9-2 2v7c0 .55.45 1 1 1h1.18c.41 1.16 1.52 2 2.82 2s2.41-.84 2.82-2h4.36c.41 1.16 1.52 2 2.82 2s2.41-.84 2.82-2H21c.55 0 1-.45 1-1v-6c0-1.66-1.34-3-3-3zM7 17.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm10 0c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zM4 12V9h4v3H4zm6 0V9h4v3h-4zm6 0V9h3.5c.83 0 1.5.67 1.5 1.5V12H16z";
            break;
        case 'tram':
            path = "M19 16.5V10c0-2.21-1.79-4-4-4h-1.5l1.62-2.16L13.88 3 12 5.5 10.12 3 8.88 3.84 10.5 6H9c-2.21 0-4 1.79-4 4v6.5C5 17.88 6.12 19 7.5 19L6 20.5v.5h12v-.5L16.5 19c1.38 0 2.5-1.12 2.5-2.5zM7.5 17c-.83 0-1.5-.67-1.5-1.5S6.67 14 7.5 14s1.5.67 1.5 1.5S8.33 17 7.5 17zm4.5-6H7V8h5v3zm5 6c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm0-6h-4V8h4v3z";
            break;
        default:
            path = "M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18-.21 0-.41-.06-.57-.18l-7.9-4.44A.991.991 0 0 1 3 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18.21 0 .41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15 6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zM19 15.91v-6.7l-6 3.37v6.71l6-3.38z"; // Cargo Box
    }
    return (
        <svg viewBox="0 0 24 24" style={{ width: '18rem', height: '18rem', ...style }} fill={svgFill}>
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

    const [isBusiestMode, setIsBusiestMode] = useState<boolean>(false);
    const [activeTab, setActiveTab] = useState<TransitType>('bus');
    const [activeLines, setActiveLines] = useState<Set<number>>(new Set());
    const [expandedLineId, setExpandedLineId] = useState<number | null>(null);
    const knownLineIds = useRef<Set<number>>(new Set());
    const [isOverflowOpen, setIsOverflowOpen] = useState(false);
    const isPickerMode = useValue(isMapPickerActive$);
    const selectedTransitLine = useValue(selectedTransitLine$);

    // Sorting States
    const [sortField, setSortField] = useState<SortField>('name');
    const [sortDesc, setSortDesc] = useState<boolean>(false);

    const activeTabs: TransitType[] = ['bus', 'train', 'subway', 'tram', 'ferry', 'cargo'];

    const sortOptions: SortField[] = ['name', 'usage', 'vehicles', 'passengers', 'waitingPassengers', 'avgWaitTime', 'length', 'stops'];

    const sortLabels: Record<SortField, string> = {
        name: 'Name',
        usage: 'Usage %',
        vehicles: 'Vehicles',
        passengers: activeTab === 'cargo' ? 'Cargo' : 'Passengers',
        waitingPassengers: 'Waiting',
        avgWaitTime: 'Avg Wait',
        length: 'Distance',
        stops: 'Stops'
    };

    let lines: TransitLine[] = [];
    try {
        if (rawData && rawData !== "[]") lines = JSON.parse(rawData);
    } catch (e) {
        console.error("[BetterTransitView] Failed to parse transitLinesData JSON:", e, rawData);
    }

    const busiestStops = React.useMemo(() => {
        const map = new Map<number, {
            id: number;
            name: string;
            totalWaiting: number;
            maxWaitTime: number;
            lines: Array<{
                id: number;
                name: string;
                color: string;
                type: TransitType;
                waiting: number;
                waitTime: number;
            }>;
        }>();

        lines.forEach(line => {
            if (line.cargo || !line.stopList) return;
            line.stopList.forEach(stop => {
                const key = (stop as any).targetId || stop.id;
                let entry = map.get(key);
                if (!entry) {
                    entry = {
                        id: stop.id,
                        name: stop.name,
                        totalWaiting: 0,
                        maxWaitTime: 0,
                        lines: []
                    };
                    map.set(key, entry);
                }
                entry.totalWaiting += (stop.waiting || 0);
                if ((stop.waitTime || 0) > entry.maxWaitTime) {
                    entry.maxWaitTime = stop.waitTime;
                }
                entry.lines.push({
                    id: line.id,
                    name: line.name,
                    color: line.color,
                    type: line.type,
                    waiting: stop.waiting || 0,
                    waitTime: stop.waitTime || 0
                });
            });
        });

        return Array.from(map.values())
            .filter(s => s.totalWaiting > 0 || s.lines.length > 0)
            .sort((a, b) => b.totalWaiting - a.totalWaiting || b.maxWaitTime - a.maxWaitTime)
            .slice(0, 15);
    }, [lines]);

    const scrollLineIntoView = React.useCallback((targetLineId: number) => {
        let attempts = 0;
        const tryScroll = () => {
            const el = document.getElementById(`transit-line-${targetLineId}`);
            if (el && el.clientHeight > 0) {
                let container: HTMLElement | null = el.parentElement;
                while (container && container !== document.body) {
                    const style = window.getComputedStyle(container);
                    const overflowY = style.overflowY;
                    const className = container.className && typeof container.className === 'string' ? container.className : '';
                    const isScrollClass = className.includes('scrollable') || className.includes('content') || className.includes('scroll');

                    if (overflowY === 'auto' || overflowY === 'scroll' || isScrollClass || (container.scrollHeight > container.clientHeight && container.clientHeight > 0)) {
                        const elRect = el.getBoundingClientRect();
                        const containerRect = container.getBoundingClientRect();
                        const relativeTop = (elRect.top - containerRect.top) + container.scrollTop;
                        const targetScroll = relativeTop - (container.clientHeight / 2) + (el.clientHeight / 2);
                        container.scrollTop = Math.max(0, targetScroll);
                        break;
                    }
                    container = container.parentElement;
                }

                el.style.transition = 'none';
                el.style.backgroundColor = 'rgba(66, 135, 245, 0.8)';

                setTimeout(() => {
                    el.style.transition = 'background-color 1.5s ease-out';
                    el.style.backgroundColor = 'rgba(255, 255, 255, 0.05)';
                }, 50);
            } else if (attempts < 20) {
                attempts++;
                setTimeout(tryScroll, 50);
            }
        };
        tryScroll();
    }, []);

    const handleJumpToLineAndStop = (lineId: number, lineType: TransitType, stopId?: number) => {
        setIsBusiestMode(false);
        const line = lines.find(l => l.id === lineId);
        if (line) {
            setActiveTab(line.cargo ? 'cargo' : (line.type === 'none' ? 'bus' : line.type));
        }
        setExpandedLineId(lineId);
        if (stopId) {
            trigger("BetterTransitView", "showVanillaLineInfo", stopId);
        }
        scrollLineIntoView(lineId);
    };

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
            styleEl.innerHTML = `
                div[class*="selected-info-panel_"] {
                    transform: translateX(495rem) !important;
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

    // Handle Escape key to request closing the UI if it's the last open screen
    /*useEffect(() => {
        if (!isVisible) return;
        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === "Escape") {
                trigger("BetterTransitView", "handleEscapeKey");
            }
        };
        window.addEventListener("keydown", handleKeyDown, true);
        return () => window.removeEventListener("keydown", handleKeyDown, true);
    }, [isVisible]);*/


    useEffect(() => {
        if (selectedTransitLine !== 0 && lines.length > 0) {
            const line = lines.find(l => l.id === selectedTransitLine);
            if (line) {
                // Force the per-line list view — the crowded-stops view never
                // renders `transit-line-${id}` rows, so scrolling there is a no-op.
                setIsBusiestMode(false);
                setActiveTab(line.cargo ? 'cargo' : (line.type === 'none' ? 'bus' : line.type));

                const targetLineId = selectedTransitLine;
                let attempts = 0;

                const tryScroll = () => {
                    const el = document.getElementById(`transit-line-${targetLineId}`);
                    if (el && el.clientHeight > 0) {
                        let container: HTMLElement | null = el.parentElement;
                        while (container && container !== document.body) {
                            const style = window.getComputedStyle(container);
                            const overflowY = style.overflowY;
                            const className = container.className && typeof container.className === 'string' ? container.className : '';
                            const isScrollClass = className.includes('scrollable') || className.includes('content') || className.includes('scroll');

                            if (overflowY === 'auto' || overflowY === 'scroll' || isScrollClass || (container.scrollHeight > container.clientHeight && container.clientHeight > 0)) {
                                const elRect = el.getBoundingClientRect();
                                const containerRect = container.getBoundingClientRect();
                                const relativeTop = (elRect.top - containerRect.top) + container.scrollTop;
                                const targetScroll = relativeTop - (container.clientHeight / 2) + (el.clientHeight / 2);
                                container.scrollTop = Math.max(0, targetScroll);
                                container.dispatchEvent(new Event('scroll', { bubbles: true }));
                                break;
                            }
                            container = container.parentElement;
                        }

                        el.style.transition = 'none';
                        el.style.backgroundColor = 'rgba(66, 135, 245, 0.8)';

                        setTimeout(() => {
                            el.style.transition = 'background-color 1.5s ease-out';
                            el.style.backgroundColor = 'rgba(255, 255, 255, 0.05)';
                        }, 50);

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

    const togglePeopleIconMode = () => {
        const nextState = !showWaitingPassengers;
        trigger("BetterTransitView", "setShowWaitingPassengers", nextState);
        setIsBusiestMode(nextState);

        if (nextState) {
            if (activeTab === 'cargo' || activeTab === 'bus') {
                setActiveTab('busiest');
            }
            setSortField('waitingPassengers');
            setSortDesc(true);
        } else {
            if (activeTab === 'busiest') {
                setActiveTab('bus');
            }
            setSortField('name');
            setSortDesc(false);
        }
    };

    const toggleExpand = (lineId: number, e: React.MouseEvent) => {
        e.stopPropagation();
        setExpandedLineId(prev => prev === lineId ? null : lineId);
    };

    const sortedLines = [...lines].filter(l => {
        if (isBusiestMode) {
            if (l.cargo) return false;
            if (activeTab === 'busiest') return true;
            return l.type === activeTab || (activeTab === 'bus' && l.type === 'none');
        } else {
            if (activeTab === 'cargo') return l.cargo;
            return !l.cargo && (l.type === activeTab || (activeTab === 'bus' && l.type === 'none'));
        }
    }).sort((a, b) => {
        let valA: any = a[sortField];
        let valB: any = b[sortField];

        if (sortField === 'length') {
            valA = a.lengthRaw || parseFloat(a.length as string) || 0;
            valB = b.lengthRaw || parseFloat(b.length as string) || 0;
        } else if (sortField === 'waitingPassengers') {
            valA = a.waitingPassengers || 0;
            valB = b.waitingPassengers || 0;
        } else if (sortField === 'avgWaitTime') {
            valA = a.avgWaitTime || 0;
            valB = b.avgWaitTime || 0;
        }

        let comparison = 0;
        if (typeof valA === 'string' && typeof valB === 'string') {
            comparison = valA.localeCompare(valB);
        } else {
            comparison = (valA as number) > (valB as number) ? 1 : ((valA as number) < (valB as number) ? -1 : 0);
        }

        if (sortDesc) comparison = -comparison;

        if (comparison === 0) {
            comparison = a.id - b.id;
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
        const targetState = lines.some(l => !activeLines.has(l.id));

        const next = new Set<number>();
        if (targetState) {
            lines.forEach(l => next.add(l.id));
        }
        setActiveLines(next);
        trigger("BetterTransitView", "setAllLinesVisible", targetState);
    };

    const panelOpacity = showInfoviewBackground ? 1.0 : 0.98;

    if (!isVisible) return null;

    return (
        <div style={{ position: 'absolute', top: 0, left: 0, width: 0, height: 0, pointerEvents: 'none' }}>
            {isPickerMode && (
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
                        width: '485rem', maxHeight: '800rem', padding: '12rem', pointerEvents: 'auto', display: 'flex', flexDirection: 'column',
                        opacity: panelOpacity,
                        backgroundImage: 'none',
                        backgroundColor: `rgba(42, 55, 83, ${panelOpacity})`,
                        backdropFilter: theme?.toolOptionsPanel ? undefined : 'blur(10px)',
                        border: '1rem solid rgba(255, 255, 255, 0.1)',
                        borderRadius: theme?.toolOptionsPanel ? undefined : '6rem',
                        boxShadow: '0 4rem 20rem rgba(0, 0, 0, 0.5)'
                    }}>

                    <div style={{ padding: '10rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <h2 style={{ margin: 0, fontSize: '16rem', fontWeight: 'bold' }}>{isBusiestMode ? "Crowded Stops" : "Transit View"}</h2>
                        <div style={{ display: 'flex', alignItems: 'center' }} id="divtopToggles">
                            {/* Hourglass Toggle for Busiest / Crowded Mode */}
                            <Tooltip tooltip={isBusiestMode ? "Switch to Line List View" : "View Crowded Stops"}>
                                <div
                                    onClick={() => setIsBusiestMode(!isBusiestMode)}
                                    style={{
                                        display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer',
                                        color: isBusiestMode ? '#fff' : '#aaa',
                                        backgroundColor: isBusiestMode ? '#4287f5' : 'rgba(255,255,255,0.1)',
                                        padding: '4rem', marginRight: '8rem', borderRadius: '4rem', transition: 'all 0.2s'
                                    }}
                                >
                                    <HourglassIcon />
                                </div>
                            </Tooltip>

                            {/* Gray Map Toggle */}
                            <Tooltip tooltip={showInfoviewBackground ? "Turn Off Gray Map" : "Turn On Gray Map"}>
                                <div onClick={() => trigger("BetterTransitView", "setShowInfoviewBackground", !showInfoviewBackground)} style={{ display: 'flex', alignItems: 'center', fontSize: '11rem', cursor: 'pointer', color: showInfoviewBackground ? '#fff' : '#aaa', backgroundColor: showInfoviewBackground ? '#4287f5' : 'rgba(255,255,255,0.1)', padding: '4rem', borderRadius: '4rem', transition: 'all 0.2s', fontWeight: showInfoviewBackground ? 'bold' : 'normal' }}>
                                    Map
                                </div>
                            </Tooltip>

                            {/* Separator */}
                            <div style={{ width: '1px', height: '16rem', backgroundColor: 'rgba(255,255,255,0.1)', marginLeft: '6rem', marginRight: '5rem' }} />

                            {/* Vehicles Toggle */}
                            <Tooltip tooltip={showTransitVehicles ? "Hide Vehicles on Map" : "Show Vehicles on Map"}>
                                <div onClick={() => trigger("BetterTransitView", "setShowTransitVehicles", !showTransitVehicles)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', color: showTransitVehicles ? '#fff' : '#aaa', backgroundColor: showTransitVehicles ? '#4287f5' : 'rgba(255,255,255,0.1)', padding: '4rem 5rem', borderRadius: '4rem', transition: 'all 0.2s' }}>
                                    <BusIcon />
                                </div>
                            </Tooltip>

                            {/* People Icon Toggle (MOVED BACK NEXT TO BUS ICON) */}
                            <Tooltip tooltip={showWaitingPassengers ? "Hide Waiting Passengers on Map" : "Show Waiting Passengers on Map"}>
                                <div onClick={() => trigger("BetterTransitView", "setShowWaitingPassengers", !showWaitingPassengers)} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', color: showWaitingPassengers ? '#fff' : '#aaa', backgroundColor: showWaitingPassengers ? '#4287f5' : 'rgba(255,255,255,0.1)', padding: '4rem 5rem', marginLeft: '3rem', borderRadius: '4rem', transition: 'all 0.2s' }}>
                                    <PeopleIcon />
                                </div>
                            </Tooltip>

                            {/* Toggle All Button */}
                            <Tooltip tooltip="Toggle visibility of all transit lines">
                                <button onClick={toggleMasterAll} style={{ backgroundColor: 'rgba(255,255,255,0.15)', border: '1rem solid rgba(255,255,255,0.3)', color: 'white', padding: '4rem 8rem', borderRadius: '4rem', cursor: 'pointer', fontSize: '11rem', textTransform: 'uppercase', marginLeft: '25rem' }}>
                                    Toggle All
                                </button>
                            </Tooltip>

                            <Tooltip tooltip="Close">
                                <button onClick={() => trigger("BetterTransitView", "toggleTransitCustom", false)} style={{ backgroundColor: ' rgba(0,0,0,0.5)', border: 'none', cursor: 'pointer', marginLeft: '15rem', padding: '4rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                    <CloseIcon />
                                </button>
                            </Tooltip>
                        </div>
                    </div>
                    {isBusiestMode ? (
                        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
                            <div style={{ padding: '8rem 15rem', color: '#aaa', fontSize: '13rem', borderBottom: '1rem solid rgba(255,255,255,0.1)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <span style={{ fontWeight: 'bold', color: '#fff' }}> </span>
                                <span style={{ fontSize: '11rem', color: '#888' }}>Ranked by waiting passengers</span>
                            </div>

                            <Scrollable
                                id="btv-busiest-stops-container"
                                vertical={true}
                                style={{ padding: '8rem 4rem 8rem 8rem', flex: 1, minHeight: 0, position: 'relative' }}
                            >
                                {busiestStops.length === 0 ? (
                                    <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No crowded stops found.</div>
                                ) : (
                                    busiestStops.map((stop, sIdx) => (
                                        <div
                                            key={stop.id || sIdx}
                                            style={{
                                                marginBottom: '10rem',
                                                backgroundColor: 'rgba(255,255,255,0.05)',
                                                borderRadius: '6rem',
                                                padding: '10rem 12rem',
                                                borderLeft: '4rem solid #ffb703',
                                                border: '1rem solid rgba(255,255,255,0.08)'
                                            }}
                                        >
                                            {/* Stop Header */}
                                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '8rem' }}>
                                                <Tooltip tooltip="Click to jump to stop location on map">
                                                    <div
                                                        onClick={() => trigger("BetterTransitView", "showVanillaLineInfo", stop.id)}
                                                        style={{ display: 'flex', alignItems: 'center', cursor: 'pointer', minWidth: 0, flex: 1, marginRight: '10rem' }}
                                                    >
                                                        {(() => {
                                                            const types = Array.from(new Set(stop.lines.map(l => l.type).filter(Boolean))) as TransitType[];
                                                            const displayTypes = types.length > 0 ? types : ['bus' as TransitType];
                                                            return (
                                                                <div style={{ marginRight: '8rem', display: 'flex', alignItems: 'center', flexShrink: 0 }}>
                                                                    {displayTypes.map(t => (
                                                                        <span key={t} style={{ marginRight: '4rem', display: 'flex', alignItems: 'center' }}>
                                                                            <TransportTypeIcon type={t} />
                                                                        </span>
                                                                    ))}
                                                                </div>
                                                            );
                                                        })()}
                                                        <span style={{ fontWeight: 'bold', fontSize: '15rem', color: '#fff', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                                                            {stop.name}
                                                        </span>
                                                    </div>
                                                </Tooltip>
                                                <Tooltip tooltip="Total Passengers Waiting at Stop">
                                                    <div style={{ display: 'flex', alignItems: 'center', color: '#fff', fontWeight: 'bold', fontSize: '13rem', flexShrink: 0 }}>
                                                        <PassengerIcon /> <span style={{ marginLeft: '4rem' }}>{stop.totalWaiting}</span>
                                                    </div>
                                                </Tooltip>
                                            </div>

                                            {/* Connected Lines List */}
                                            <div style={{ display: 'flex', flexDirection: 'column', backgroundColor: 'rgba(0,0,0,0.25)', borderRadius: '4rem', padding: '6rem 8rem' }}>
                                                {stop.lines.map((line, lIdx) => (
                                                    <div
                                                        key={line.id || lIdx}
                                                        onClick={() => handleJumpToLineAndStop(line.id, line.type, stop.id)}
                                                        style={{
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'space-between',
                                                            fontSize: '12rem',
                                                            cursor: 'pointer',
                                                            padding: '4rem 8rem',
                                                            marginBottom: lIdx < stop.lines.length - 1 ? '4rem' : '0',
                                                            borderRadius: '4rem',
                                                            transition: 'background-color 0.15s ease'
                                                        }}
                                                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.1)'}
                                                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                                    >
                                                        <div style={{ display: 'flex', alignItems: 'center', minWidth: 0, flex: 1, marginRight: '10rem' }}>
                                                            <div style={{
                                                                width: '4rem',
                                                                height: '16rem',
                                                                backgroundColor: line.color || '#4287f5',
                                                                borderRadius: '2rem',
                                                                flexShrink: 0,
                                                                marginRight: '8rem'
                                                            }} />
                                                            <span style={{
                                                                fontSize: '13rem',
                                                                fontWeight: 600,
                                                                color: '#fff',
                                                                whiteSpace: 'nowrap',
                                                                overflow: 'hidden',
                                                                textOverflow: 'ellipsis'
                                                            }}>
                                                                {line.name}
                                                            </span>
                                                        </div>

                                                        <div style={{ display: 'flex', alignItems: 'center', flexShrink: 0 }}>
                                                            <Tooltip tooltip="Passengers Waiting on this Line">
                                                                <span style={{ display: 'flex', alignItems: 'center', marginRight: '14rem', color: line.waiting > 0 ? '#ffffff' : '#888', fontWeight: line.waiting > 0 ? '600' : 'normal', fontSize: '11rem' }}>
                                                                    <PassengerIcon /> <span style={{ marginLeft: '4rem' }}>{line.waiting}</span>
                                                                </span>
                                                            </Tooltip>
                                                            {line.waitTime > 0 && (
                                                                <Tooltip tooltip="Average Wait Time on this Line">
                                                                    <span style={{ display: 'flex', alignItems: 'center', marginRight: '10rem', color: '#aaa', fontSize: '11rem' }}>
                                                                        <WaitTimeIcon /> <span style={{ marginLeft: '4rem' }}>{line.waitTime}m</span>
                                                                    </span>
                                                                </Tooltip>
                                                            )}
                                                        </div>
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                    ))
                                )}
                            </Scrollable>
                        </div>
                    ) : (
                        <>
                            <div style={{ display: 'flex', borderBottom: '1rem solid rgba(255,255,255,0.1)', position: 'relative' }}>
                                {activeTabs.map((tab) => (
                                    <button key={tab} onClick={() => { setActiveTab(tab); setIsOverflowOpen(false); }} style={{ flex: 1, padding: '10rem 0', cursor: 'pointer', fontSize: '13rem', background: activeTab === tab ? 'rgba(255,255,255,0.1)' : 'transparent', border: 'none', color: activeTab === tab ? 'white' : '#888', borderBottom: activeTab === tab ? '2rem solid #4287f5' : '2rem solid transparent' }}>
                                        {tab === 'busiest' ? 'Busiest' : (tab.charAt(0).toUpperCase() + tab.slice(1))}
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
                                        <Tooltip tooltip={sortDesc ? "Descending Order" : "Ascending Order"}>
                                            <button onClick={() => setSortDesc(!sortDesc)} style={{ background: 'rgba(255,255,255,0.05)', border: '1rem solid rgba(255,255,255,0.1)', borderRadius: '4rem', color: '#fff', cursor: 'pointer', padding: '4rem 8rem', marginLeft: '1rem', fontSize: '12rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                {sortDesc ? 'DESC ↓' : 'ASC ↑'}
                                            </button>
                                        </Tooltip>
                                    </div>

                                    {/* TOOL BUTTON */}
                                    <Tooltip tooltip={`Equip ${activeTab === 'busiest' ? 'bus' : activeTab} tool`}>
                                        <button
                                            onClick={() => trigger("BetterTransitView", "activateTransitTool", activeTab === 'busiest' ? 'bus' : activeTab)}
                                            style={{ marginLeft: '15rem', backgroundColor: '#4287f5', border: 'none', borderRadius: '4rem', color: 'white', padding: '4rem 10rem', cursor: 'pointer', display: 'flex', alignItems: 'center', fontSize: '12rem', fontWeight: 'bold' }}
                                        >
                                            <ToolIcon /> &nbsp;Tool
                                        </button>
                                    </Tooltip>

                                    {/* PICKER BUTTON */}
                                    <Tooltip tooltip={isPickerMode ? "Cancel line picker" : "Pick a line on the map"}>
                                        <button
                                            onClick={() => {
                                                trigger("BetterTransitView", "toggleMapPicker", !isPickerMode);
                                            }}
                                            style={{ marginLeft: '3rem', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem 8rem', borderRadius: '4rem', cursor: 'pointer', backgroundColor: isPickerMode ? 'rgba(255, 0, 0, 0.5)' : 'rgba(255,255,255,0.05)', color: isPickerMode ? 'white' : '#aaa', border: '1rem solid rgba(255,255,255,0.1)' }}
                                        >
                                            <CrosshairIcon />
                                        </button>
                                    </Tooltip>

                                </div>
                                <Tooltip tooltip="Toggle visibility of all lines in this tab">
                                    <div onClick={toggleTabAll} style={{ display: 'flex', alignItems: 'center', fontSize: '13rem', cursor: 'pointer', color: '#fff' }}>
                                        Toggle Tab <CustomCheckbox checked={allVisibleInTab} onChange={() => { }} />
                                    </div>
                                </Tooltip>
                            </div>

                            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>
                                <Scrollable
                                    id="btv-transit-list-container"
                                    vertical={true}
                                    style={{ padding: '8rem 4rem 8rem 8rem', flex: 1, minHeight: 0, position: 'relative' }}
                                >
                                    {sortedLines.length === 0 ? (
                                        <div style={{ padding: '20rem', textAlign: 'center', color: '#666', fontSize: '13rem' }}>No lines found.</div>
                                    ) : sortedLines.map(line => {
                                        const isExpanded = expandedLineId === line.id;
                                        return (
                                            <React.Fragment key={line.id}>
                                                <div id={`transit-line-${line.id}`} onClick={() => toggleLine(line.id)} style={{ display: 'flex', alignItems: 'center', padding: '10rem', marginBottom: isExpanded ? '0' : '8rem', backgroundColor: 'rgba(255,255,255,0.05)', borderRadius: isExpanded ? '6rem 6rem 0 0' : '6rem', borderLeft: `4rem solid ${line.color}`, cursor: 'pointer' }}>

                                                    {/* Expand Arrow Button */}
                                                    <Tooltip tooltip={isExpanded ? "Collapse Stops" : "Expand Stops"}>
                                                        <div
                                                            onClick={(e) => toggleExpand(line.id, e)}
                                                            style={{
                                                                marginRight: '6rem',
                                                                padding: '4rem',
                                                                cursor: 'pointer',
                                                                display: 'flex',
                                                                alignItems: 'center',
                                                                justifyContent: 'center',
                                                                color: '#aaa',
                                                                borderRadius: '4rem',
                                                                transition: 'all 0.15s ease'
                                                            }}
                                                            onMouseEnter={(e) => e.currentTarget.style.color = '#fff'}
                                                            onMouseLeave={(e) => e.currentTarget.style.color = '#aaa'}
                                                        >
                                                            {isExpanded ? <ChevronDownIcon /> : <ChevronRightIcon />}
                                                        </div>
                                                    </Tooltip>

                                                    {/* Type Icon is dynamically added in Cargo or Busiest Tab */}
                                                    {(activeTab === 'cargo' || activeTab === 'busiest') && (
                                                        <Tooltip tooltip={`Type: ${line.type}`}>
                                                            <div style={{ marginRight: '8rem', display: 'flex', alignItems: 'center' }}>
                                                                <TransportTypeIcon type={line.type} />
                                                            </div>
                                                        </Tooltip>
                                                    )}

                                                    <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
                                                        <div style={{ fontWeight: 'bold', fontSize: '16rem', marginBottom: '6rem', display: 'flex', alignItems: 'center' }}>
                                                            <span style={{ whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                                                                {line.name} &nbsp;
                                                            </span>
                                                            <Tooltip tooltip="Inspect Line">
                                                                <div
                                                                    onClick={(e) => {
                                                                        e.stopPropagation();
                                                                        trigger("BetterTransitView", "showVanillaLineInfo", line.id);
                                                                    }}
                                                                    style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '4rem', borderRadius: '4rem', transition: 'background-color 0.1s', cursor: 'pointer', backgroundColor: 'rgba(255,255,255,0.05)' }}
                                                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.15)'}
                                                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.05)'}
                                                                >
                                                                    <SearchIcon />
                                                                </div>
                                                            </Tooltip>
                                                        </div>


                                                        <div style={{ fontSize: '13rem', color: '#bbb', display: 'flex', flexWrap: 'wrap', rowGap: '8rem' }}>

                                                            {/* Prominent Passengers Waiting & Avg Wait Time in Busiest Mode */}
                                                            {isBusiestMode ? (
                                                                <>
                                                                    <Tooltip tooltip="Waiting Passengers">
                                                                        <span style={{ display: 'flex', alignItems: 'center', width: '62rem', color: (line.waitingPassengers || 0) > 0 ? '#ffffff' : '#bbb', fontWeight: (line.waitingPassengers || 0) > 0 ? 'bold' : 'normal' }}>
                                                                            <PassengerIcon /> <span style={{ marginLeft: '3rem' }}>{line.waitingPassengers || 0}</span>
                                                                        </span>
                                                                    </Tooltip>

                                                                    <Tooltip tooltip="Average Wait Time">
                                                                        <span style={{ display: 'flex', alignItems: 'center', width: '62rem', color: '#bbb' }}>
                                                                            <WaitTimeIcon /> <span style={{ marginLeft: '3rem' }}>{line.avgWaitTime || 0}m</span>
                                                                        </span>
                                                                    </Tooltip>

                                                                    <Tooltip tooltip="Stops">
                                                                        <span style={{ display: 'flex', alignItems: 'center', width: '52rem' }}>
                                                                            <StopIcon /> <span style={{ marginLeft: '3rem' }}>{line.stops || 0}</span>
                                                                        </span>
                                                                    </Tooltip>

                                                                    <Tooltip tooltip={line.hasShortage ? "Vehicle Shortage: Not enough vehicles available from depot" : (line.isDispatching ? "Vehicle(s) on the way from depot" : "Active Vehicles")}>
                                                                        <span style={{
                                                                            display: 'flex', alignItems: 'center', width: '55rem',
                                                                            color: line.hasShortage ? '#ff4d4d' : (line.isDispatching ? '#ffd700' : '#bbb'),
                                                                            fontWeight: line.hasShortage || line.isDispatching ? 'bold' : 'normal'
                                                                        }}
                                                                        >
                                                                            <VehicleIcon /> <span style={{ marginLeft: '3rem' }}>{line.vehicles}</span>

                                                                            {line.hasShortage ? <span style={{ marginLeft: '3rem' }}><WarningIcon /></span> : (line.isDispatching ? <span style={{ marginLeft: '3rem' }}><DispatchIcon /></span> : null)}
                                                                        </span>
                                                                    </Tooltip>
                                                                </>
                                                            ) : (
                                                                <>
                                                                    <Tooltip tooltip="Distance">
                                                                        <span style={{ display: 'flex', alignItems: 'center', width: line.cargo ? '74rem' : '78rem' }}>
                                                                            <LengthIcon /> <span style={{ marginLeft: '3rem' }}>{line.length}</span>
                                                                        </span>
                                                                    </Tooltip>

                                                                    <Tooltip tooltip="Stops">
                                                                        <span style={{ display: 'flex', alignItems: 'center', width: line.cargo ? '50rem' : '55rem' }}>
                                                                            <StopIcon /> <span style={{ marginLeft: '3rem' }}>{line.stops || 0}</span>
                                                                        </span>
                                                                    </Tooltip>

                                                                    <Tooltip tooltip={line.hasShortage ? "Vehicle Shortage: Not enough vehicles available from depot" : (line.isDispatching ? "Vehicle(s) on the way from depot" : "Active Vehicles")}>
                                                                        <span style={{
                                                                            display: 'flex', alignItems: 'center', width: line.cargo ? '60rem' : '65rem',
                                                                            color: line.hasShortage ? '#ff4d4d' : (line.isDispatching ? '#ffd700' : '#bbb'),
                                                                            fontWeight: line.hasShortage || line.isDispatching ? 'bold' : 'normal'
                                                                        }}
                                                                        >
                                                                            <VehicleIcon /> <span style={{ marginLeft: '3rem' }}>{line.vehicles}</span>

                                                                            {line.hasShortage ? <span style={{ marginLeft: '3rem' }}><WarningIcon /></span> : (line.isDispatching ? <span style={{ marginLeft: '3rem' }}><DispatchIcon /></span> : null)}
                                                                        </span>
                                                                    </Tooltip>

                                                                    {line.cargo ? (
                                                                        <Tooltip tooltip="Cargo Transported">
                                                                            <span style={{ display: 'flex', alignItems: 'center', width: '66rem' }}>
                                                                                <CargoIcon /> <span style={{ marginLeft: '3rem' }}>{((line.passengers || 0) / 1000).toFixed(0)} t</span>
                                                                            </span>
                                                                        </Tooltip>
                                                                    ) : (
                                                                        <Tooltip tooltip="Passengers Transported">
                                                                            <span style={{ display: 'flex', alignItems: 'center', width: '70rem' }}>
                                                                                <PassengerIcon /> <span style={{ marginLeft: '3rem' }}>{line.passengers || 0}</span>
                                                                            </span>
                                                                        </Tooltip>
                                                                    )}

                                                                    <Tooltip tooltip="Line Usage">
                                                                        <span style={{ display: 'flex', alignItems: 'center', width: line.cargo ? '50rem' : '55rem' }}>
                                                                            <UsageIcon /> <span style={{ marginLeft: '3rem' }}>{line.usage}%</span>
                                                                        </span>
                                                                    </Tooltip>
                                                                </>
                                                            )}
                                                        </div>
                                                    </div>

                                                    <Tooltip tooltip="Toggle line visibility">
                                                        <div style={{ marginLeft: '15rem', flexShrink: 0 }}>
                                                            <CustomCheckbox checked={activeLines.has(line.id)} onChange={() => { }} />
                                                        </div>
                                                    </Tooltip>
                                                </div>

                                                {/* Expanded Stops List */}
                                                {isExpanded && (
                                                    <div style={{ marginBottom: '8rem', backgroundColor: 'rgba(0,0,0,0.3)', borderRadius: '0 0 6rem 6rem', overflow: 'hidden', borderLeft: `4rem solid ${line.color}`, borderBottom: '1rem solid rgba(255,255,255,0.05)', borderRight: '1rem solid rgba(255,255,255,0.05)' }}>
                                                        {(!line.stopList || line.stopList.length === 0) ? (
                                                            <div style={{ padding: '8rem 15rem', color: '#888', fontSize: '12rem', fontStyle: 'italic' }}>No stops found for this line.</div>
                                                        ) : (
                                                            line.stopList.map((stop, sIdx) => (
                                                                <div
                                                                    key={stop.id || sIdx}
                                                                    onClick={(e) => {
                                                                        e.stopPropagation();
                                                                        trigger("BetterTransitView", "showVanillaLineInfo", stop.id);
                                                                    }}
                                                                    style={{
                                                                        display: 'flex',
                                                                        alignItems: 'center',
                                                                        justifyContent: 'space-between',
                                                                        padding: '7rem 14rem 7rem 20rem',
                                                                        borderBottom: sIdx < line.stopList!.length - 1 ? '1rem solid rgba(255,255,255,0.05)' : 'none',
                                                                        fontSize: '13rem',
                                                                        cursor: 'pointer',
                                                                        transition: 'background-color 0.15s ease'
                                                                    }}
                                                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.08)'}
                                                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                                                >
                                                                    <div style={{ display: 'flex', alignItems: 'center', minWidth: 0, flex: 1, marginRight: '12rem', overflow: 'hidden' }}>
                                                                        <Tooltip tooltip="Jump to stop location on map">
                                                                            <div style={{ marginRight: '6rem', display: 'flex', alignItems: 'center', opacity: 0.8, flexShrink: 0 }}>
                                                                                <StopIcon />
                                                                            </div>
                                                                        </Tooltip>
                                                                        <span
                                                                            style={{
                                                                                whiteSpace: 'nowrap',
                                                                                overflow: 'hidden',
                                                                                textOverflow: 'ellipsis',
                                                                                color: '#fff',
                                                                                fontWeight: 500,
                                                                                maxWidth: '140rem',
                                                                                flexShrink: 0
                                                                            }}
                                                                        >
                                                                            {stop.name}
                                                                        </span>
                                                                        {stop.connectingLines && stop.connectingLines.length > 0 && (
                                                                            <div style={{ display: 'flex', alignItems: 'center', marginLeft: '6rem', flexShrink: 0, overflow: 'hidden' }}>
                                                                                {stop.connectingLines.map((connLine, cIdx) => {
                                                                                    const count = stop.connectingLines!.length;
                                                                                    let pillMaxWidth = '105rem';
                                                                                    if (count === 2) pillMaxWidth = '55rem';
                                                                                    else if (count === 3) pillMaxWidth = '36rem';
                                                                                    const isTextlessPill = count >= 4;

                                                                                    return (
                                                                                        <Tooltip key={connLine.id || cIdx} tooltip={`${connLine.name}${connLine.type ? ` (${connLine.type})` : ''}`}>
                                                                                            <span
                                                                                                onClick={(e) => {
                                                                                                    e.stopPropagation();
                                                                                                    handleJumpToLineAndStop(connLine.id, line.type, stop.id);
                                                                                                }}
                                                                                                style={isTextlessPill ? {
                                                                                                    width: '24rem',
                                                                                                    height: '11rem',
                                                                                                    borderRadius: '4rem',
                                                                                                    backgroundColor: connLine.color || '#4287f5',
                                                                                                    display: 'inline-block',
                                                                                                    cursor: 'pointer',
                                                                                                    boxShadow: '0 1rem 3rem rgba(0,0,0,0.4)',
                                                                                                    border: '1rem solid rgba(255,255,255,0.3)',
                                                                                                    flexShrink: 0,
                                                                                                    marginRight: '4rem'
                                                                                                } : {
                                                                                                    backgroundColor: connLine.color || '#4287f5',
                                                                                                    color: '#fff',
                                                                                                    fontSize: '10rem',
                                                                                                    fontWeight: 'bold',
                                                                                                    padding: '1.5rem 6rem',
                                                                                                    borderRadius: '8rem',
                                                                                                    whiteSpace: 'nowrap',
                                                                                                    cursor: 'pointer',
                                                                                                    boxShadow: '0 1rem 3rem rgba(0,0,0,0.4)',
                                                                                                    textShadow: '0 1rem 2rem rgba(0,0,0,0.5)',
                                                                                                    flexShrink: 0,
                                                                                                    marginRight: '4rem',
                                                                                                    maxWidth: pillMaxWidth,
                                                                                                    overflow: 'hidden',
                                                                                                    textOverflow: 'ellipsis'
                                                                                                }}
                                                                                            >
                                                                                                {!isTextlessPill && connLine.name}
                                                                                            </span>
                                                                                        </Tooltip>
                                                                                    );
                                                                                })}
                                                                            </div>
                                                                        )}
                                                                        {stop.nearbyLines && stop.nearbyLines.length > 0 && (
                                                                            <div style={{ display: 'flex', alignItems: 'center', marginLeft: '6rem', flexShrink: 0 }}>
                                                                                {stop.nearbyLines.map((nearbyLine, nIdx) => (
                                                                                    <Tooltip key={nearbyLine.id || nIdx} tooltip={`${nearbyLine.name} (${nearbyLine.type || 'transit'})`}>
                                                                                        <span
                                                                                            onClick={(e) => {
                                                                                                e.stopPropagation();
                                                                                                handleJumpToLineAndStop(nearbyLine.id, nearbyLine.type || 'bus', stop.id);
                                                                                            }}
                                                                                            style={{
                                                                                                display: 'inline-flex',
                                                                                                alignItems: 'center',
                                                                                                justifyContent: 'center',
                                                                                                cursor: 'pointer',
                                                                                                flexShrink: 0,
                                                                                                marginRight: '4rem',
                                                                                                opacity: 0.75,
                                                                                                transition: 'opacity 0.15s ease'
                                                                                            }}
                                                                                            onMouseEnter={(e) => e.currentTarget.style.opacity = '1'}
                                                                                            onMouseLeave={(e) => e.currentTarget.style.opacity = '0.75'}
                                                                                        >
                                                                                            <TransportTypeIcon type={nearbyLine.type || 'bus'} style={{ width: '15rem', height: '15rem', fill: nearbyLine.color || '#4287f5' }} />
                                                                                        </span>
                                                                                    </Tooltip>
                                                                                ))}
                                                                            </div>
                                                                        )}
                                                                    </div>
                                                                    <div style={{ display: 'flex', alignItems: 'center', flexShrink: 0, marginLeft: 'auto', paddingLeft: '10rem' }}>
                                                                        {!line.cargo && (
                                                                            <>
                                                                                <Tooltip tooltip="Passengers Waiting">
                                                                                    <span style={{ display: 'flex', alignItems: 'center', marginRight: '14rem', color: stop.waiting > 0 ? '#ffffff' : '#888', fontWeight: stop.waiting > 0 ? '600' : 'normal', fontSize: '12rem' }}>
                                                                                        <PassengerIcon /> <span style={{ marginLeft: '4rem' }}>{stop.waiting}</span>
                                                                                    </span>
                                                                                </Tooltip>
                                                                                {stop.waitTime > 0 && (
                                                                                    <Tooltip tooltip="Average Wait Time">
                                                                                        <span style={{ display: 'flex', alignItems: 'center', color: '#aaa', fontSize: '12rem' }}>
                                                                                            <WaitTimeIcon /> <span style={{ marginLeft: '4rem' }}>{stop.waitTime}m</span>
                                                                                        </span>
                                                                                    </Tooltip>
                                                                                )}
                                                                            </>
                                                                        )}
                                                                    </div>
                                                                </div>
                                                            ))
                                                        )}
                                                    </div>
                                                )}
                                            </React.Fragment>
                                        );
                                    })}
                                </Scrollable>
                            </div>
                        </>
                    )}
                </div>
            </div>
        </div>
    );
};