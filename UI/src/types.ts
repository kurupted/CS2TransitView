export type TransitType = 'bus' | 'train' | 'subway' | 'tram' | 'ferry' | 'airplane' | 'ship' | 'cargo' | 'none';

export type SortField = 'name' | 'usage' | 'vehicles' | 'passengers' | 'length' | 'stops';

export interface TransitLine {
    id: number;
    type: TransitType;
    name: string;
    color: string;
    vehicles: number;
    passengers: number;
    length: string;
    lengthRaw?: number;
    usage: number;
    cargo: boolean;
    visible: boolean;
    stops: number;
}
