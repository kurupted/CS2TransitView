export type TransitType = 'busiest' | 'bus' | 'train' | 'subway' | 'tram' | 'ferry' | 'airplane' | 'ship' | 'cargo' | 'none';

export type SortField = 'name' | 'usage' | 'vehicles' | 'passengers' | 'waitingPassengers' | 'avgWaitTime' | 'length' | 'stops';

export interface ConnectingLineData {
    id: number;
    name: string;
    color: string;
}

export interface TransitStopData {
    id: number;
    name: string;
    waiting: number;
    waitTime: number;
    connectingLines?: ConnectingLineData[];
}

export interface TransitLine {
    id: number;
    type: TransitType;
    name: string;
    color: string;
    vehicles: number;        // The active spawned count
    isDispatching?: boolean;
    hasShortage?: boolean;   // True if depot is out of vehicles
    passengers: number;
    waitingPassengers?: number;
    avgWaitTime?: number;
    length: string;
    lengthRaw?: number;
    usage: number;
    cargo: boolean;
    visible: boolean;
    stops: number;
    stopList?: TransitStopData[];
}

