#!/bin/bash

# replace this with your own protoc
protoc \
		--csharp_out="$(PWD)" \
		--csharp_opt=file_extension=.gen.cs \
		-I="$(PWD)" \
		"$(PWD)/index.proto"
