FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/CallCadence.UI/CallCadence.UI.csproj", "src/CallCadence.UI/"]
COPY ["src/CallCadence.Models/CallCadence.Models.csproj", "src/CallCadence.Models/"]
RUN dotnet restore "src/CallCadence.UI/CallCadence.UI.csproj"
COPY . .
WORKDIR "/src/src/CallCadence.UI"
RUN dotnet publish "CallCadence.UI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CallCadence.UI.dll"]
