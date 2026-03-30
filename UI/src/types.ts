export interface SegmentActivity {
    none: number;
    shopping: number;
    leisure: number;
    goingHome: number;
    goingToWork: number;
    movingIn: number;
    movingAway: number;
    school: number;
    transporting: number;
    returning: number;
    tourism: number;
    other: number;
    services: number;
}

export interface Entity {
    index: number;
    version: number;
}

export interface Theme {
    entity: Entity;
    name: string;
    icon: string;
}