FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore MarketOtomasyon/MarketOtomasyon.csproj
# Build and publish a release
RUN dotnet publish MarketOtomasyon/MarketOtomasyon.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "MarketOtomasyon.dll"]
