# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["OrderAccumulator/OrderAccumulator.sln", "./"]
COPY ["OrderAccumulator/src/", "./src/"]

RUN dotnet restore "OrderAccumulator.sln"
RUN dotnet build "OrderAccumulator.sln" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/OrderAccumulator/OrderAccumulator.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
COPY OrderAccumulator/fix.cfg ./fix.cfg

EXPOSE 9999
ENTRYPOINT ["dotnet", "OrderAccumulator.dll"]
