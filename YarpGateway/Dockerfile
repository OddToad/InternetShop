# Стейдж рантайма
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Стейдж сборки
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем файл проекта прямо в корень рабочей директории Docker
COPY ["YarpGateway.csproj", "./"]
RUN dotnet restore "YarpGateway.csproj"

# Копируем все остальные файлы проекта
COPY . .
RUN dotnet build "YarpGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Стейдж публикации
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "YarpGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Финальный стейдж
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YarpGateway.dll"]