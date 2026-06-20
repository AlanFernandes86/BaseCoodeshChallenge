# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrderAccumulator/OrderAccumulator.sln", "./"]
COPY ["OrderAccumulator/src/", "./src/"]

RUN find ./src -type d -name obj -exec rm -rf {} + 2>/dev/null || true
RUN find ./src -type d -name bin -exec rm -rf {} + 2>/dev/null || true
RUN dotnet restore "OrderAccumulator.sln"
RUN dotnet build "OrderAccumulator.sln" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/OrderAccumulator/OrderAccumulator.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
COPY OrderAccumulator/fix.cfg ./fix.cfg
COPY OrderAccumulator/FIX44.xml ./FIX44.xml

EXPOSE 9999
ENTRYPOINT ["dotnet", "OrderAccumulator.dll"]
