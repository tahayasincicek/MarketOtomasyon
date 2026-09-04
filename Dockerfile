FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /App

# Proje dosyasi once kopyalanir. Kaynak kod degisip paketler degismediginde
# NuGet restore katmani Docker cache'inden kullanilir.
COPY MarketOtomasyon/MarketOtomasyon.csproj MarketOtomasyon/
RUN dotnet restore MarketOtomasyon/MarketOtomasyon.csproj

COPY MarketOtomasyon/ MarketOtomasyon/

ARG BUILD_CONFIGURATION=Release
RUN dotnet publish MarketOtomasyon/MarketOtomasyon.csproj \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env --chown=app:app /App/out .

RUN mkdir -p /var/lib/marketotomasyon/keys \
    /App/wwwroot/urun-resim \
    /App/Loglar \
    && chown -R app:app /App /var/lib/marketotomasyon

# Ortam adi acikca yaziliyor: cerez politikasi, HSTS ve log seviyesi
# buna bagli. Yanlislikla Development'a dusen bir konteynerde oturum
# cerezi HTTP uzerinden de gonderilir.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV VeriKoruma__AnahtarKlasoru=/var/lib/marketotomasyon/keys

EXPOSE 8080
# Container yenilendiginde oturum anahtarlari, indirilen urun resimleri ve
# dosya loglari kaybolmasin. Uretimde bu noktalara adlandirilmis volume veya
# harici kalici depolama baglanir.
VOLUME ["/var/lib/marketotomasyon/keys", "/App/wwwroot/urun-resim", "/App/Loglar"]

# Baglanti dizesi imaja GOMULMEZ; calistirirken -e ile verilir:
#   docker run -e "ConnectionStrings__MarketDb=Server=...;..." marketotomasyon

USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD ["dotnet", "MarketOtomasyon.dll", "healthcheck"]

ENTRYPOINT ["dotnet", "MarketOtomasyon.dll"]
