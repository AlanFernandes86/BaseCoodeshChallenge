# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrderGenerator.Backend/OrderGenerator.Backend.sln", "./"]
COPY ["OrderGenerator.Backend/src/", "./src/"]
# Copy config files that the .csproj references with ../../fix.cfg and ../../FIX44.xml
COPY ["OrderGenerator.Backend/fix.cfg", "./fix.cfg"]
COPY ["OrderGenerator.Backend/FIX44.xml", "./FIX44.xml"]

RUN find ./src -type d -name obj -exec rm -rf {} + 2>/dev/null || true
RUN find ./src -type d -name bin -exec rm -rf {} + 2>/dev/null || true
RUN dotnet restore "src/OrderGenerator.WebAPI/OrderGenerator.WebAPI.csproj"
RUN dotnet build "src/OrderGenerator.WebAPI/OrderGenerator.WebAPI.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/OrderGenerator.WebAPI/OrderGenerator.WebAPI.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Configure PT-BR culture
RUN apt-get update && apt-get install -y locales && rm -rf /var/lib/apt/lists/* \
    && sed -i '/pt_BR.UTF-8/s/^# //g' /etc/locale.gen \
    && locale-gen pt_BR.UTF-8

ENV LANG=pt_BR.UTF-8
ENV LANGUAGE=pt_BR:pt
ENV LC_ALL=pt_BR.UTF-8

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "OrderGenerator.WebAPI.dll"]
