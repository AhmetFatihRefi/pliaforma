# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje/manifest dosyaları → restore katmanı önbelleklenir.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Pliaforma.Domain/Pliaforma.Domain.csproj src/Pliaforma.Domain/
COPY src/Pliaforma.Application/Pliaforma.Application.csproj src/Pliaforma.Application/
COPY src/Pliaforma.Persistence/Pliaforma.Persistence.csproj src/Pliaforma.Persistence/
COPY src/Pliaforma.Infrastructure/Pliaforma.Infrastructure.csproj src/Pliaforma.Infrastructure/
COPY src/Pliaforma.Api/Pliaforma.Api.csproj src/Pliaforma.Api/
RUN dotnet restore src/Pliaforma.Api/Pliaforma.Api.csproj

COPY . .
RUN dotnet publish src/Pliaforma.Api/Pliaforma.Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Pliaforma.Api.dll"]
