#!/bin/bash

# replace this with your own protoc
protoc \
    -I="$(pwd)" \
		--plugin=protoc-gen-dclunity=../../dcl-protoc-csharp-plugin/index.js \
		--dclunity_out="$(pwd)" \
		--csharp_out="$(pwd)" \
		--csharp_opt=file_extension=.gen.cs \
		-I="$(pwd)" \
		"$(pwd)/api.proto"