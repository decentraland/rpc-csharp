import { RpcClientPort } from "@dcl/rpc";
import { Book } from "./api";
export declare const createBookServiceClient: <Context>(clientPort: RpcClientPort) => import("@dcl/rpc/dist/codegen-types").RawClient<import("@dcl/rpc/dist/codegen-types").FromTsProtoServiceDefinition<{
    readonly name: "BookService";
    readonly fullName: "BookService";
    readonly methods: {
        readonly getBook: {
            readonly name: "GetBook";
            readonly requestType: {
                encode(message: import("./api").GetBookRequest, writer?: import("protobufjs").Writer): import("protobufjs").Writer;
                decode(input: Uint8Array | import("protobufjs").Reader, length?: number): import("./api").GetBookRequest;
                fromJSON(object: any): import("./api").GetBookRequest;
                toJSON(message: import("./api").GetBookRequest): unknown;
                fromPartial<I extends {
                    isbn?: number;
                } & {
                    isbn?: number;
                } & Record<Exclude<keyof I, "isbn">, never>>(object: I): import("./api").GetBookRequest;
            };
            readonly requestStream: false;
            readonly responseType: {
                encode(message: Book, writer?: import("protobufjs").Writer): import("protobufjs").Writer;
                decode(input: Uint8Array | import("protobufjs").Reader, length?: number): Book;
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
                encode(message: import("./api").QueryBooksRequest, writer?: import("protobufjs").Writer): import("protobufjs").Writer;
                decode(input: Uint8Array | import("protobufjs").Reader, length?: number): import("./api").QueryBooksRequest;
                fromJSON(object: any): import("./api").QueryBooksRequest;
                toJSON(message: import("./api").QueryBooksRequest): unknown;
                fromPartial<I_2 extends {
                    authorPrefix?: string;
                } & {
                    authorPrefix?: string;
                } & Record<Exclude<keyof I_2, "authorPrefix">, never>>(object: I_2): import("./api").QueryBooksRequest;
            };
            readonly requestStream: false;
            readonly responseType: {
                encode(message: Book, writer?: import("protobufjs").Writer): import("protobufjs").Writer;
                decode(input: Uint8Array | import("protobufjs").Reader, length?: number): Book;
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
}>, Context>;
export declare const runClient: () => void;
