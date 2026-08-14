FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DAS.DocumentService.csproj", "./"]
RUN dotnet restore "DAS.DocumentService.csproj"
COPY . .
RUN dotnet publish "DAS.DocumentService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5002
EXPOSE 5002
ENTRYPOINT ["dotnet", "DAS.DocumentService.dll"]
