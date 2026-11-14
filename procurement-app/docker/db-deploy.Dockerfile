# One-off job: builds the dacpac from db/Procurement.Database and publishes it to the sqlserver service.
# Build context: procurement-app/. The connection password comes from SA_PASSWORD at run time.
FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN dotnet tool install -g microsoft.sqlpackage --version 170.5.96
ENV PATH="${PATH}:/root/.dotnet/tools"
WORKDIR /src
COPY Directory.Build.props ./
COPY db/Procurement.Database/ db/Procurement.Database/
RUN dotnet build db/Procurement.Database/Procurement.Database.sqlproj -c Release
CMD sqlpackage /Action:Publish \
    /SourceFile:/src/db/Procurement.Database/bin/Release/Procurement.Database.dacpac \
    /TargetConnectionString:"Server=sqlserver,1433;User ID=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Database=procurement"
