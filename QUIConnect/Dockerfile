#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine AS base
WORKDIR /app

FROM ahendrix/dotnet-sdk-plus:latest AS build
WORKDIR /src
COPY ["QUIConnect/QUIConnect.csproj", "QUIConnect/"]
RUN dotnet restore "QUIConnect/QUIConnect.csproj"
COPY . .
WORKDIR "/src/QUIConnect"
RUN dotnet build "QUIConnect.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QUIConnect.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QUIConnect.dll"]