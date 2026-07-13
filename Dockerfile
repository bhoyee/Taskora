FROM node:24-alpine AS web-build
WORKDIR /web
COPY ["src/Taskora.Web/package.json", "src/Taskora.Web/package-lock.json", "./"]
RUN npm ci
COPY ["src/Taskora.Web", "."]
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Taskora.Api/Taskora.Api.csproj", "src/Taskora.Api/"]
COPY ["src/Taskora.Application/Taskora.Application.csproj", "src/Taskora.Application/"]
COPY ["src/Taskora.Domain/Taskora.Domain.csproj", "src/Taskora.Domain/"]
COPY ["src/Taskora.Infrastructure/Taskora.Infrastructure.csproj", "src/Taskora.Infrastructure/"]
RUN dotnet restore "src/Taskora.Api/Taskora.Api.csproj"
COPY . .
COPY --from=web-build /web/dist /src/src/Taskora.Web/dist
RUN dotnet publish "src/Taskora.Api/Taskora.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Taskora.Api.dll"]
