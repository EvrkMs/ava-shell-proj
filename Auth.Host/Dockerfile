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

# Generate a self-signed certificate for Kestrel (for HTTPS binding)
ARG KESTREL_CERT_PASSWORD=changeit
ENV KESTREL_CERT_PASSWORD=$KESTREL_CERT_PASSWORD
RUN apt-get update \
    && apt-get install -y --no-install-recommends openssl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /https \
    && echo "[req]\nreq_extensions = v3_req\ndistinguished_name = req_distinguished_name\n[req_distinguished_name]\n[v3_req]\nkeyUsage = keyEncipherment, dataEncipherment\nextendedKeyUsage = serverAuth\nsubjectAltName = @alt_names\n[alt_names]\nDNS.1 = localhost\nDNS.2 = auth\nDNS.3 = auth-host" > /https/openssl.cnf \
    && openssl req -x509 -nodes -newkey rsa:2048 -keyout /https/https.key -out /https/https.crt -subj "/CN=auth" -days 3650 -config /https/openssl.cnf -extensions v3_req \
    && openssl pkcs12 -export -out /https/https.pfx -inkey /https/https.key -in /https/https.crt -passout pass:${KESTREL_CERT_PASSWORD}

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=https://+:5001 \
    ASPNETCORE_Kestrel__Certificates__Default__Path=/https/https.pfx \
    ASPNETCORE_Kestrel__Certificates__Default__Password=$KESTREL_CERT_PASSWORD \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

COPY --from=build /app/publish ./

# No ports published by default; can be exposed to other containers
EXPOSE 5001

ENTRYPOINT ["dotnet", "Auth.Host.dll"]
