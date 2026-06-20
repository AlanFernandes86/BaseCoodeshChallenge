# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OrderGenerator.Backend/OrderGenerator.Backend.sln", "./"]
COPY ["OrderGenerator.Backend/src/", "./src/"]

RUN find ./src -type d -name obj -exec rm -rf {} + 2>/dev/null || true
RUN find ./src -type d -name bin -exec rm -rf {} + 2>/dev/null || true
RUN dotnet restore "OrderGenerator.Backend.sln"
RUN dotnet build "OrderGenerator.Backend.sln" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/OrderGenerator.WebAPI/OrderGenerator.WebAPI.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
COPY OrderGenerator.Backend/fix.cfg ./fix.cfg

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "OrderGenerator.WebAPI.dll"]
