import { RpcServerPort } from "@dcl/rpc";
import { Book } from "./api";
export declare type TestContext = {
    hardcodedDatabase: Book[];
};
export declare function registerBookServiceServerImplementation(port: RpcServerPort<TestContext>): void;
export declare const context: TestContext;
export declare const runServer: () => void;
