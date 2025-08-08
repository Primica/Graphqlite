# Dockerfile pour GraphQLite API
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Graphqlite.csproj", "."]
RUN dotnet restore "Graphqlite.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "Graphqlite.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Graphqlite.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Créer le répertoire pour la base de données
RUN mkdir -p /app/data
VOLUME ["/app/data"]

# Variables d'environnement
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV DatabasePath=/app/data/graphqlite.gqlite

ENTRYPOINT ["dotnet", "Graphqlite.dll", "--server"]
