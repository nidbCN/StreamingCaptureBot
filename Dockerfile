FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["./StreamingCaptureBot.Abstraction/StreamingCaptureBot.Abstraction.csproj", "./StreamingCaptureBot.Abstraction/"]
COPY ["./StreamingCaptureBot.Hosting/StreamingCaptureBot.Hosting.csproj", "./StreamingCaptureBot.Hosting/"]
COPY ["./StreamingCaptureBot.Utils/StreamingCaptureBot.Utils.csproj", "./StreamingCaptureBot.Utils/"]
COPY ["./StreamingCaptureBot.Impl/Tencent/StreamingCaptureBot.Impl.Tencent.csproj", "./StreamingCaptureBot.Impl/Tencent/"]
COPY ["./StreamingCaptureBot.Impl/Lagrange/StreamingCaptureBot.Impl.Lagrange.csproj", "./StreamingCaptureBot.Impl/Lagrange/"]
COPY ["./Lagrange.Core/Lagrange.Core/Lagrange.Core.csproj", "./Lagrange.Core/Lagrange.Core/"]
COPY ["./FfMpegLib.Net/FfMpegLib.Net.csproj", "./FfMpegLib.Net"]

RUN dotnet restore "./StreamingCaptureBot.Hosting/StreamingCaptureBot.Hosting.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./StreamingCaptureBot.Hosting/StreamingCaptureBot.Hosting.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./StreamingCaptureBot.Hosting/StreamingCaptureBot.Hosting.csproj" \
    -c $BUILD_CONFIGURATION \
    --self-contained true \
    --runtime linux-x64 \
    -o /app/publish

FROM registry.cn-beijing.aliyuncs.com/nidb-cr/streaming-capture-bot-base:n7.0.2-26 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./StreamingCaptureBot.Hosting"]
