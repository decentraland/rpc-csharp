#!/bin/bash

# replace this with your own protoc
protoc \
		--plugin=../node_modules/.bin/protoc-gen-ts_proto \
		--csharp_out="$(PWD)" \
		--csharp_opt=file_extension=.gen.cs \
		-I="$(PWD)" \
		"$(PWD)/api.proto"