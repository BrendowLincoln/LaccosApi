FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ src/

RUN --mount=type=cache,id=lacoos-nuget,target=/root/.nuget/packages \
    dotnet publish src/Host/Lacoos.Api/Lacoos.Api.csproj -c Release -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# A pasta nasce com o dono certo: o volume nomeado herda essa permissão na primeira montagem.
RUN mkdir -p /app/data/uploads && chown -R $APP_UID:$APP_UID /app/data

COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    FileStorage__RootPath=/app/data/uploads

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "Lacoos.Api.dll"]
