FROM node:24-alpine AS web-build
WORKDIR /web
COPY ["src/TodoApp.Web/package.json", "src/TodoApp.Web/package-lock.json", "./"]
RUN npm ci
COPY ["src/TodoApp.Web", "."]
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/TodoApp.Api/TodoApp.Api.csproj", "src/TodoApp.Api/"]
COPY ["src/TodoApp.Application/TodoApp.Application.csproj", "src/TodoApp.Application/"]
COPY ["src/TodoApp.Domain/TodoApp.Domain.csproj", "src/TodoApp.Domain/"]
COPY ["src/TodoApp.Infrastructure/TodoApp.Infrastructure.csproj", "src/TodoApp.Infrastructure/"]
RUN dotnet restore "src/TodoApp.Api/TodoApp.Api.csproj"
COPY . .
COPY --from=web-build /web/dist /src/src/TodoApp.Web/dist
RUN dotnet publish "src/TodoApp.Api/TodoApp.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TodoApp.Api.dll"]
