# ------------------------------------------------------------
# Stage 1: Build Frontend (Vite + React)
# ------------------------------------------------------------
FROM node:22-alpine AS frontend-builder
WORKDIR /app/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ------------------------------------------------------------
# Stage 2: Build & Publish Backend (.NET 10 Web API)
# ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-builder
WORKDIR /src

COPY backend/KanbanStudio.slnx ./backend/
COPY backend/src/KanbanStudio.Api/KanbanStudio.Api.csproj ./backend/src/KanbanStudio.Api/
RUN dotnet restore backend/src/KanbanStudio.Api/KanbanStudio.Api.csproj

COPY backend/src/KanbanStudio.Api/ ./backend/src/KanbanStudio.Api/
RUN dotnet publish backend/src/KanbanStudio.Api/KanbanStudio.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------------------------------------
# Stage 3: Runtime Container
# ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/data /app/frontend/dist

COPY --from=backend-builder /app/publish ./
COPY --from=frontend-builder /app/frontend/dist ./frontend/dist

ENV ASPNETCORE_URLS=http://+:8000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV STATIC_DIR=/app/frontend/dist
ENV DATABASE_PATH=/app/data/pm.db

EXPOSE 8000
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "KanbanStudio.Api.dll"]
