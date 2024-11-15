FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS base
USER root
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["./CameraCaptureBot.Core/CameraCaptureBot.Core.csproj", "./CameraCaptureBot.Core/"]
RUN mkdir -p ./Lagrange.Core/Lagrange.Core
COPY ["./Lagrange.Core/Lagrange.Core/Lagrange.Core.csproj", "./Lagrange.Core/Lagrange.Core/"]
RUN dotnet restore "./CameraCaptureBot.Core/CameraCaptureBot.Core.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./CameraCaptureBot.Core/CameraCaptureBot.Core.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CameraCaptureBot.Core/CameraCaptureBot.Core.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM alpine AS ffmpeg
WORKDIR /tmp
RUN apk add --no-cache curl xz
ARG FFMPEG_URL=https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n7.1-latest-linux64-gpl-shared-7.1.tar.xz
RUN curl -L $FFMPEG_URL -o ffmpeg.tar.xz && \
    mkdir -p ffmpeg && \
    tar -xJf ffmpeg.tar.xz -C ffmpeg --strip-components=1 "ffmpeg-n7.1-latest-linux64-gpl-shared-7.1/lib" && \
    ls ffmpeg

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=ffmpeg /tmp/ffmpeg/lib/* /usr/lib
ENTRYPOINT ["dotnet", "CameraCaptureBot.Core.dll"]
