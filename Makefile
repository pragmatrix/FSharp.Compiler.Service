MSB=msbuild.exe /m /verbosity:m /nologo
NUGET=nuget.exe

VER=1.0.1
NAME=FSharp.Compiler.Service

FEEDSOURCE=https://www.myget.org/F/livepipes/api/v2/package

conf=Release

.PHONY: build
build:
	${MSB} src/fsharp/${NAME}/${NAME}.fsproj /p:Configuration=${conf}

.PHONY: nuget
nuget: build
	cd src/fsharp/${NAME} && ${NUGET} pack ${NAME}.fsproj -Version ${VER} -Prop Configuration=${conf} 

.PHONY: publish
publish: nuget
	cd src/fsharp/${NAME} && ${NUGET} push ${NAME}.${VER}.nupkg ${MYGETAPIKEY} -source ${FEEDSOURCE} 

