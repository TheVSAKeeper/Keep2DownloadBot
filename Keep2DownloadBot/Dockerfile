# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS base
USER $APP_UID
WORKDIR /app
RUN mkdir -p logs temp && chown $APP_UID:$APP_UID logs temp

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

RUN apk add --no-cache clang build-base zlib-dev

COPY ["Keep2DownloadBot/Keep2DownloadBot.csproj", "Keep2DownloadBot/"]

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore "Keep2DownloadBot/Keep2DownloadBot.csproj" -r linux-musl-x64

COPY . .
WORKDIR "/src/Keep2DownloadBot"

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish "Keep2DownloadBot.csproj" \
    -c $BUILD_CONFIGURATION \
    -r linux-musl-x64 \
    -o /app/publish \
    --self-contained true \
    /p:UseAppHost=true

FROM base AS final
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["./Keep2DownloadBot"]
