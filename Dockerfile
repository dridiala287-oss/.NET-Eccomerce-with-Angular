# ---- 1) Build Angular ----
FROM node:22 AS client
ARG STRIPE_PK=""
WORKDIR /src/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN if [ -n "$STRIPE_PK" ]; then sed -i "s|pk_test_[A-Za-z0-9]*|$STRIPE_PK|g" src/environments/environment.ts; fi
RUN npx ng build

# ---- 2) Build .NET ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY API/API.csproj API/
RUN dotnet restore API/API.csproj
COPY Core/ Core/
COPY Infrastructure/ Infrastructure/
COPY API/ API/
COPY --from=client /src/API/wwwroot API/wwwroot
RUN dotnet publish API/API.csproj -c Release -o /app/publish --no-restore

# ---- 3) Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "API.dll"]
