FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/TodoApp.Api/TodoApp.Api.csproj", "src/TodoApp.Api/"]
COPY ["src/TodoApp.Application/TodoApp.Application.csproj", "src/TodoApp.Application/"]
COPY ["src/TodoApp.Domain/TodoApp.Domain.csproj", "src/TodoApp.Domain/"]
COPY ["src/TodoApp.Infrastructure/TodoApp.Infrastructure.csproj", "src/TodoApp.Infrastructure/"]
RUN dotnet restore "src/TodoApp.Api/TodoApp.Api.csproj"
COPY . .
RUN dotnet publish "src/TodoApp.Api/TodoApp.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TodoApp.Api.dll"]
