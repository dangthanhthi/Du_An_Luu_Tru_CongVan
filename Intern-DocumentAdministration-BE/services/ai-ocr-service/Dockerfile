# Sử dụng base image Alpine 3.20 (siêu nhẹ, tương thích .NET 10.0)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 5006

# Cài đặt Tesseract, Data Tiếng Việt & Ghostscript (bắt buộc cho Magick.NET đọc PDF) qua apk
RUN apk update && apk add --no-cache \
    tesseract-ocr \
    tesseract-ocr-data-vie \
    ghostscript \
    fontconfig

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["*.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV TESSDATA_PREFIX=/usr/share/tessdata

ENTRYPOINT ["dotnet", "ai-ocr-service.dll"]