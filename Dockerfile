FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as debug

#install debugger for NET Core
RUN apt-get update
RUN apt-get install -y unzip
RUN curl -sSL https://aka.ms/getvsdbgsh | /bin/sh /dev/stdin -v latest -l /vsdbg

COPY . /app
WORKDIR /app

RUN dotnet restore
RUN dotnet build
RUN dotnet publish src/TodoBackend.Api/TodoBackend.Api.csproj -c Release -o ../bin

ENTRYPOINT [ "dotnet", "/bin/TodoBackend.Api.dll" ]
