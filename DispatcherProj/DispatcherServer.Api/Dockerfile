FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DispatcherProj/DispatcherServer.Core/DispatcherServer.Core.csproj", "DispatcherProj/DispatcherServer.Core/"]
COPY ["DispatcherProj/DispatcherServer.Plugins/DispatcherServer.Plugins.csproj", "DispatcherProj/DispatcherServer.Plugins/"]
COPY ["DispatcherProj/DispatcherServer.Messaging/DispatcherServer.Messaging.csproj", "DispatcherProj/DispatcherServer.Messaging/"]
COPY ["DispatcherProj/DispatcherServer.Persistence/DispatcherServer.Persistence.csproj", "DispatcherProj/DispatcherServer.Persistence/"]
COPY ["DispatcherProj/DispatcherServer.Api/DispatcherServer.Api.csproj", "DispatcherProj/DispatcherServer.Api/"]
RUN dotnet restore "DispatcherProj/DispatcherServer.Api/DispatcherServer.Api.csproj"

COPY . .
RUN dotnet publish "DispatcherProj/DispatcherServer.Api/DispatcherServer.Api.csproj" \
	--configuration Release \
	--no-restore \
	--output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DispatcherServer.Api.dll"]
