# Procurement.Bff serving the Angular build from wwwroot on port 8080. Build context: procurement-app/.

# 1. Angular build (npm ci installs exactly what package-lock.json pins).
FROM node:22-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npx ng build --configuration production

# 2. BFF publish. The placeholder wwwroot is dropped; the Angular output replaces it below.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Procurement.Bff/Procurement.Bff.csproj src/Procurement.Bff/
RUN dotnet restore src/Procurement.Bff/Procurement.Bff.csproj
COPY src/Procurement.Bff/ src/Procurement.Bff/
RUN dotnet publish src/Procurement.Bff/Procurement.Bff.csproj -c Release -o /app --no-restore \
    && rm -rf /app/wwwroot

# 3. Runtime.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY --from=web /web/dist/web/browser ./wwwroot/
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Procurement.Bff.dll"]
