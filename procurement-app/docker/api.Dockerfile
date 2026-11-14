# Procurement.Api: Minimal API on internal port 8080. Build context: procurement-app/.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Procurement.Domain/Procurement.Domain.csproj src/Procurement.Domain/
COPY src/Procurement.Api/Procurement.Api.csproj src/Procurement.Api/
RUN dotnet restore src/Procurement.Api/Procurement.Api.csproj
COPY src/Procurement.Domain/ src/Procurement.Domain/
COPY src/Procurement.Api/ src/Procurement.Api/
RUN dotnet publish src/Procurement.Api/Procurement.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Procurement.Api.dll"]
