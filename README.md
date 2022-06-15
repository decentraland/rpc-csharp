# rpc-csharp

rpc-csharp is a implementation of https://github.com/decentraland/rpc in C#

Progress tracking in https://github.com/decentraland/sdk/issues/321

The RPC Client is not implemented because it was not needed.

# Dependencies

- protoc (3.12 or later)
- UniTask (2.25 or later)

# UPM Package

Go to the Package Manager in Unity and add the following link:
```
https://github.com/decentraland/rpc-csharp.git?path=rpc-csharp/src
```
or add
```
"com": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask" 
```
to Packages/manifest.json.

# Code generation

To generate the code, you need to use the following plugin:
https://github.com/decentraland/protoc-gen-csharp

The built code is in this repo in the `dcl-protoc-csharp-plugin` folder.

## Usage

```
protoc \
    -I="$(pwd)" \
    --plugin=protoc-gen-dclunity=../../dcl-protoc-csharp-plugin/index.js \
    --dclunity_out="$(pwd)" \
    "$(pwd)/api.proto"
```

## RPC Test project

You can use the following project to test the RPC

https://github.com/kuruk-mm/dcl-rpc-test

This can connect the RPC Client with the RPC Server in the `rpc-csharp-demo` project