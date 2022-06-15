#!/bin/bash

# replace this with your own protoc
protoc \
		--csharp_out="$(pwd)" \
		--csharp_opt=file_extension=.gen.cs \
		-I="$(pwd)" \
		"$(pwd)/index.proto"
