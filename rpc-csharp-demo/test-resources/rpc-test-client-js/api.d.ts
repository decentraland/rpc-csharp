import * as _m0 from "protobufjs/minimal";
export declare const protobufPackage = "";
export interface Book {
    isbn: number;
    title: string;
    author: string;
}
export interface GetBookRequest {
    isbn: number;
}
export interface QueryBooksRequest {
    authorPrefix: string;
}
export declare const Book: {
    encode(message: Book, writer?: _m0.Writer): _m0.Writer;
    decode(input: _m0.Reader | Uint8Array, length?: number): Book;
    fromJSON(object: any): Book;
    toJSON(message: Book): unknown;
    fromPartial<I extends {
        isbn?: number;
        title?: string;
        author?: string;
    } & {
        isbn?: number;
        title?: string;
        author?: string;
    } & Record<Exclude<keyof I, keyof Book>, never>>(object: I): Book;
};
export declare const GetBookRequest: {
    encode(message: GetBookRequest, writer?: _m0.Writer): _m0.Writer;
    decode(input: _m0.Reader | Uint8Array, length?: number): GetBookRequest;
    fromJSON(object: any): GetBookRequest;
    toJSON(message: GetBookRequest): unknown;
    fromPartial<I extends {
        isbn?: number;
    } & {
        isbn?: number;
    } & Record<Exclude<keyof I, "isbn">, never>>(object: I): GetBookRequest;
};
export declare const QueryBooksRequest: {
    encode(message: QueryBooksRequest, writer?: _m0.Writer): _m0.Writer;
    decode(input: _m0.Reader | Uint8Array, length?: number): QueryBooksRequest;
    fromJSON(object: any): QueryBooksRequest;
    toJSON(message: QueryBooksRequest): unknown;
    fromPartial<I extends {
        authorPrefix?: string;
    } & {
        authorPrefix?: string;
    } & Record<Exclude<keyof I, "authorPrefix">, never>>(object: I): QueryBooksRequest;
};
export declare type BookServiceDefinition = typeof BookServiceDefinition;
export declare const BookServiceDefinition: {
    readonly name: "BookService";
    readonly fullName: "BookService";
    readonly methods: {
        readonly getBook: {
            readonly name: "GetBook";
            readonly requestType: {
                encode(message: GetBookRequest, writer?: _m0.Writer): _m0.Writer;
                decode(input: _m0.Reader | Uint8Array, length?: number): GetBookRequest;
                fromJSON(object: any): GetBookRequest;
                toJSON(message: GetBookRequest): unknown;
                fromPartial<I extends {
                    isbn?: number;
                } & {
                    isbn?: number;
                } & Record<Exclude<keyof I, "isbn">, never>>(object: I): GetBookRequest;
            };
            readonly requestStream: false;
            readonly responseType: {
                encode(message: Book, writer?: _m0.Writer): _m0.Writer;
                decode(input: _m0.Reader | Uint8Array, length?: number): Book;
                fromJSON(object: any): Book;
                toJSON(message: Book): unknown;
                fromPartial<I_1 extends {
                    isbn?: number;
                    title?: string;
                    author?: string;
                } & {
                    isbn?: number;
                    title?: string;
                    author?: string;
                } & Record<Exclude<keyof I_1, keyof Book>, never>>(object: I_1): Book;
            };
            readonly responseStream: false;
            readonly options: {};
        };
        readonly queryBooks: {
            readonly name: "QueryBooks";
            readonly requestType: {
                encode(message: QueryBooksRequest, writer?: _m0.Writer): _m0.Writer;
                decode(input: _m0.Reader | Uint8Array, length?: number): QueryBooksRequest;
                fromJSON(object: any): QueryBooksRequest;
                toJSON(message: QueryBooksRequest): unknown;
                fromPartial<I_2 extends {
                    authorPrefix?: string;
                } & {
                    authorPrefix?: string;
                } & Record<Exclude<keyof I_2, "authorPrefix">, never>>(object: I_2): QueryBooksRequest;
            };
            readonly requestStream: false;
            readonly responseType: {
                encode(message: Book, writer?: _m0.Writer): _m0.Writer;
                decode(input: _m0.Reader | Uint8Array, length?: number): Book;
                fromJSON(object: any): Book;
                toJSON(message: Book): unknown;
                fromPartial<I_1 extends {
                    isbn?: number;
                    title?: string;
                    author?: string;
                } & {
                    isbn?: number;
                    title?: string;
                    author?: string;
                } & Record<Exclude<keyof I_1, keyof Book>, never>>(object: I_1): Book;
            };
            readonly responseStream: true;
            readonly options: {};
        };
    };
};
declare type Builtin = Date | Function | Uint8Array | string | number | boolean | undefined;
export declare type DeepPartial<T> = T extends Builtin ? T : T extends Array<infer U> ? Array<DeepPartial<U>> : T extends ReadonlyArray<infer U> ? ReadonlyArray<DeepPartial<U>> : T extends {} ? {
    [K in keyof T]?: DeepPartial<T[K]>;
} : Partial<T>;
declare type KeysOfUnion<T> = T extends T ? keyof T : never;
export declare type Exact<P, I extends P> = P extends Builtin ? P : P & {
    [K in keyof P]: Exact<P[K], I[K]>;
} & Record<Exclude<keyof I, KeysOfUnion<P>>, never>;
export {};
