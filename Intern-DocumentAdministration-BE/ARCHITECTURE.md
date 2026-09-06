# Backend Architecture

The repository is organized first by deployable service. Each service is currently one ASP.NET Core project, matching the size of the codebase and keeping local development straightforward.

A service must not reference another service's project or database schema. Cross-service collaboration happens through HTTP contracts.

## Service folder convention

Services use lightweight folder-based layers:

```text
service-name/
├── Controllers/       HTTP endpoints
├── Models/
│   ├── DTOs/          API and application contracts
│   └── Entities/      persisted business entities
├── Data/              EF Core DbContext and persistence setup
├── Services/          use cases and business operations
│   └── Integration/   clients for other services
├── Migrations/        EF Core migrations
├── Common/            shared concerns within this service
├── Program.cs         composition root
└── <Service>.csproj   single service project
```

Not every small service needs every folder. Add a folder when it has a real responsibility rather than creating empty layers.

## Document service commands

```powershell
dotnet build services/document-service/DocumentService.csproj
dotnet run --project services/document-service/DocumentService.csproj
dotnet ef database update --project services/document-service/DocumentService.csproj
```

Docker Compose builds from `services/document-service`, and the executable remains `DocumentService.dll`.

## Dependency guidance

- Controllers should delegate business operations to services.
- Services may coordinate persistence and integration clients.
- Data contains EF Core-specific code.
- Models should not depend on controllers.
- Services must communicate with other microservices through HTTP clients, never through project references or cross-schema queries.

If a service becomes substantially larger, its folders can later become separate Domain, Application, Infrastructure, and API projects. That split should be driven by complexity and testing needs rather than applied uniformly now.
