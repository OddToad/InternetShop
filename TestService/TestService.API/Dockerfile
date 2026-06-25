FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
# Работаем прямо из корня сборки, чтобы пути вида '../../' или 'TestService.API/' разрешались корректно
WORKDIR /

# 1. Копируем файлы описания проектов, сохраняя структуру папок солюшена
COPY ["TestService.API/TestService.API.csproj", "TestService.API/"]
COPY ["TestService.Infrastructure/TestService.Infrastructure.csproj", "TestService.Infrastructure/"]
COPY ["TestService.Application/TestService.Application.csproj", "TestService.Application/"]
COPY ["TestService.Domain/TestService.Domain.csproj", "TestService.Domain/"]

# 2. Восстанавливаем зависимости из папки проекта API
RUN dotnet restore "TestService.API/TestService.API.csproj"

# 3. Копируем абсолютно весь исходный код микросервиса
COPY . .

# 4. Переходим в папку API для финальной сборки
WORKDIR "/TestService.API"
RUN dotnet build "TestService.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Стейдж публикации
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TestService.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Рантайм стейдж
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TestService.API.dll"]