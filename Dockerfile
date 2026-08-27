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
COPY --from=build-env --chown=app:app /App/out .

RUN mkdir -p /var/lib/marketotomasyon/keys \
    && chown -R app:app /App /var/lib/marketotomasyon

# Ortam adi acikca yaziliyor: cerez politikasi, HSTS ve log seviyesi
# buna bagli. Yanlislikla Development'a dusen bir konteynerde oturum
# cerezi HTTP uzerinden de gonderilir.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV VeriKoruma__AnahtarKlasoru=/var/lib/marketotomasyon/keys

VOLUME ["/var/lib/marketotomasyon/keys"]

# Baglanti dizesi imaja GOMULMEZ; calistirirken -e ile verilir:
#   docker run -e "ConnectionStrings__MarketDb=Server=...;..." marketotomasyon

USER app

ENTRYPOINT ["dotnet", "MarketOtomasyon.dll"]
