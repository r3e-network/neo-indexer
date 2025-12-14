export type Datum = Record<string, any>;

export type DatumElement = Element & { __datum__?: Datum };

