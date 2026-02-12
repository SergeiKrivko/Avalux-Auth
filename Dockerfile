FROM node:22.12-alpine AS angular-build
WORKDIR /src

COPY UI/package*.json ./
RUN npm ci

COPY UI/ .
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Api/AvaluxAuth.Api/AvaluxAuth.Api.csproj", "Api/AvaluxAuth.Api/"]
COPY ["Api/AvaluxAuth.Abstractions/AvaluxAuth.Abstractions.csproj", "Api/AvaluxAuth.Abstractions/"]
COPY ["Api/AvaluxAuth.Models/AvaluxAuth.Models.csproj", "Api/AvaluxAuth.Models/"]
COPY ["Api/AvaluxAuth.DataAccess/AvaluxAuth.DataAccess.csproj", "Api/AvaluxAuth.DataAccess/"]
COPY ["Api/AvaluxAuth.Utils/AvaluxAuth.Utils.csproj", "Api/AvaluxAuth.Utils/"]
COPY ["Api/AvaluxAuth.Providers/AvaluxAuth.Providers.csproj", "Api/AvaluxAuth.Providers/"]
COPY ["Api/AvaluxAuth.Services/AvaluxAuth.Services.csproj", "Api/AvaluxAuth.Services/"]
RUN dotnet restore "Api/AvaluxAuth.Api/AvaluxAuth.Api.csproj"
COPY . .
COPY --from=angular-build /src/dist/UI/browser ./Api/AvaluxAuth.Api/wwwroot/
WORKDIR "/src/Api/AvaluxAuth.Api"
RUN dotnet build "./AvaluxAuth.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AvaluxAuth.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AvaluxAuth.Api.dll"]
