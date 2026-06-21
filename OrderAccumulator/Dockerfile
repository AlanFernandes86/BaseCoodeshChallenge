# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrderAccumulator/OrderAccumulator.sln", "./"]
COPY ["OrderAccumulator/src/", "./src/"]
# Copy config files that the .csproj references with ../../fix.cfg and ../../FIX44.xml
COPY ["OrderAccumulator/fix.cfg", "./fix.cfg"]
COPY ["OrderAccumulator/FIX44.xml", "./FIX44.xml"]

RUN find ./src -type d -name obj -exec rm -rf {} + 2>/dev/null || true
RUN find ./src -type d -name bin -exec rm -rf {} + 2>/dev/null || true
RUN dotnet restore "src/OrderAccumulator/OrderAccumulator.csproj"
RUN dotnet build "src/OrderAccumulator/OrderAccumulator.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/OrderAccumulator/OrderAccumulator.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Configure PT-BR culture
RUN apt-get update && apt-get install -y locales && rm -rf /var/lib/apt/lists/* \
    && sed -i '/pt_BR.UTF-8/s/^# //g' /etc/locale.gen \
    && locale-gen pt_BR.UTF-8

ENV LANG=pt_BR.UTF-8
ENV LANGUAGE=pt_BR:pt
ENV LC_ALL=pt_BR.UTF-8

COPY --from=publish /app/publish .

EXPOSE 9999
ENTRYPOINT ["dotnet", "OrderAccumulator.dll"]
