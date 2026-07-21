FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/CallCadence.API/CallCadence.API.csproj", "src/CallCadence.API/"]
COPY ["src/CallCadence.Models/CallCadence.Models.csproj", "src/CallCadence.Models/"]
RUN dotnet restore "src/CallCadence.API/CallCadence.API.csproj"
COPY . .
WORKDIR "/src/src/CallCadence.API"
RUN dotnet publish "CallCadence.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CallCadence.API.dll"]
