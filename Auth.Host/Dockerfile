# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for better layer caching
COPY ComputerAdminAuth.sln ./
COPY Auth.Host/Auth.Host.csproj Auth.Host/
COPY Auth.Infrastructure/Auth.Infrastructure.csproj Auth.Infrastructure/
COPY Auth.EntityFramework/Auth.EntityFramework.csproj Auth.EntityFramework/
COPY Auth.Application/Auth.Application.csproj Auth.Application/
COPY Auth.Domain/Auth.Domain.csproj Auth.Domain/
COPY Auth.TelegramAuth/Auth.TelegramAuth.csproj Auth.TelegramAuth/
COPY Auth.Shared.Contracts/Auth.Shared.Contracts.csproj Auth.Shared.Contracts/

RUN dotnet restore ./Auth.Host/Auth.Host.csproj --nologo

# Copy the rest and publish
COPY . ./
RUN dotnet publish ./Auth.Host/Auth.Host.csproj -c Release -o /app/publish --no-restore --nologo

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Configure ASP.NET Core to listen HTTP internally; HTTPS is terminated at reverse proxy (nginx)
ENV ASPNETCORE_URLS=https://+:5001 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

COPY --from=build /app/publish ./

# No ports published by default; can be exposed to other containers
EXPOSE 5001

ENTRYPOINT ["dotnet", "Auth.Host.dll"]
