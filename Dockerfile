FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Aircrafts/AircraftSimulator/AircraftSimulator.Core/AircraftSimulator.Core.csproj", "Aircrafts/AircraftSimulator/AircraftSimulator.Core/"]
COPY ["Aircrafts/AircraftSimulator/AircraftSimulator.Plugins/AircraftSimulator.Plugins.csproj", "Aircrafts/AircraftSimulator/AircraftSimulator.Plugins/"]
COPY ["Aircrafts/AircraftSimulator/AircraftSimulator.Host/AircraftSimulator.Host.csproj", "Aircrafts/AircraftSimulator/AircraftSimulator.Host/"]
RUN dotnet restore "Aircrafts/AircraftSimulator/AircraftSimulator.Host/AircraftSimulator.Host.csproj"

COPY . .
RUN dotnet publish "Aircrafts/AircraftSimulator/AircraftSimulator.Host/AircraftSimulator.Host.csproj" \
	--configuration Release \
	--no-restore \
	--output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "AircraftSimulator.Host.dll"]
