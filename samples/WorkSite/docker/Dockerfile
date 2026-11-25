# The WorkSite web application: the ASP.NET Core backend, which also serves the built frontend
# (the static files in frontend-built/, which publishing carries along). The build context is the
# published application (samples/WorkSite/artifacts/publish), which `WorkSite.BuildFrontend run`
# prepares. WORKDIR is the content root, where the backend looks for frontend-built/.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY . .
USER $APP_UID
ENTRYPOINT ["dotnet", "WorkSite.Api.dll"]
