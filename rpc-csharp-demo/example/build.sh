#!/bin/bash

# replace this with your own protoc
protoc \
    -I="$(PWD)" \
		--plugin=protoc-gen-dclunity=../../dcl-protoc-csharp-plugin/index.js \
		--dclunity_out="$(PWD)" \
		--csharp_out="$(PWD)" \
		--csharp_opt=file_extension=.gen.cs \
		-I="$(PWD)" \
		"$(PWD)/api.proto"