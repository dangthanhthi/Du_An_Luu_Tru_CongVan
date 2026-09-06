FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base

WORKDIR /app

EXPOSE 8080

RUN apk update && apk add --no-cache \
    tesseract-ocr \
    tesseract-ocr-data-vie \
    tesseract-ocr-data-eng \
    ghostscript \
    fontconfig

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

WORKDIR /src

COPY ["*.csproj", "./"]

RUN dotnet restore

COPY . .

RUN dotnet build \
    -c Release \
    -o /app/build \
    --no-restore

FROM build AS publish

RUN dotnet publish \
    -c Release \
    -o /app/publish \
    --no-restore

FROM base AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENV TESSDATA_PREFIX=/usr/share/tessdata

ENTRYPOINT ["dotnet", "ai-ocr-service.dll"]