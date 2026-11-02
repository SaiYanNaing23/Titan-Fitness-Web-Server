# Multi-stage Dockerfile for GymManagement Blazor Web App (.NET 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy Solution & csproj files first for optimal Docker layer caching
COPY GymManagement.sln .
COPY src/GymManagement.Domain/GymManagement.Domain.csproj src/GymManagement.Domain/
COPY src/GymManagement.Application/GymManagement.Application.csproj src/GymManagement.Application/
COPY src/GymManagement.Infrastructure/GymManagement.Infrastructure.csproj src/GymManagement.Infrastructure/
COPY src/GymManagement.Web/GymManagement.Web.csproj src/GymManagement.Web/
COPY tests/GymManagement.UnitTests/GymManagement.UnitTests.csproj tests/GymManagement.UnitTests/

RUN dotnet restore

# Copy source code and build
COPY . .
WORKDIR /app/src/GymManagement.Web
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GymManagement.Web.dll"]
