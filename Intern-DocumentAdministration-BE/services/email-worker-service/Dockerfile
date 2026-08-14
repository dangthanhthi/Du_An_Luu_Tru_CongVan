# Copy this file into each service folder as "Dockerfile",
# then replace "ServiceName.dll" with that service's actual output
# (e.g. AuthService.dll, DocumentService.dll ...)
#
# Tag note: SDK and runtime (aspnet) images use different tagging
# schemes. Use "major.minor" only (e.g. 10.0) for BOTH images -
# a patch-level tag like 10.0.302 exists for the SDK but not for
# the runtime image, and will fail with "not found".
# Keep this version consistent with <TargetFramework> in the
# service's .csproj.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "EmailWorkerService.dll"]