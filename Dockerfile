# syntax=docker/dockerfile:1
FROM node:24.22.1-bookworm-slim AS frontend
WORKDIR /frontend
COPY src/PortalTarefas.Frontend/package.json src/PortalTarefas.Frontend/package-lock.json ./
RUN npm ci
COPY src/PortalTarefas.Frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS backend
WORKDIR /source
COPY global.json ./
COPY src/PortalTarefas.Web/PortalTarefas.Web.csproj src/PortalTarefas.Web/
RUN dotnet restore src/PortalTarefas.Web/PortalTarefas.Web.csproj
COPY src/PortalTarefas.Web/ src/PortalTarefas.Web/
ARG BUILD_VERSION=local
RUN dotnet publish src/PortalTarefas.Web/PortalTarefas.Web.csproj -c Release --no-restore -o /publish /p:UseAppHost=false /p:InformationalVersion=${BUILD_VERSION} /p:IncludeSourceRevisionInInformationalVersion=false
COPY --from=frontend /frontend/dist/ /publish/wwwroot/app/

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
ARG BUILD_VERSION=local
LABEL org.opencontainers.image.title="Portal de Tarefas" \
      org.opencontainers.image.revision="${BUILD_VERSION}"
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R ${APP_UID}:${APP_UID} /home/app/.aspnet
WORKDIR /app
COPY --from=backend --chown=$APP_UID:$APP_UID /publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_HTTPS_PORTS=8443 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080 8443
USER $APP_UID
ENTRYPOINT ["dotnet", "PortalTarefas.Web.dll"]
