# The WorkSite web application: the ASP.NET Core backend, which also serves the built frontend
# (static files in wwwroot). The build context is the published application
# (samples/WorkSite/artifacts/publish), which `WorkSite.BuildFrontend run` prepares.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY . .
USER $APP_UID
ENTRYPOINT ["dotnet", "WorkSite.Api.dll"]
