FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Orchestrator/Orchestrator.csproj .
RUN dotnet restore
COPY src/Orchestrator/ .
RUN dotnet publish -c Release -o /out --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet","Orchestrator.dll"]
