FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first, restore, then copy the rest.
# Docker caches the restore layer, so editing a .cs file does not
# re-download every NuGet package.
COPY src/Garage/Garage.csproj src/Garage/
RUN dotnet restore src/Garage/Garage.csproj

COPY src/ src/
RUN dotnet publish src/Garage/Garage.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# DejaVu is what the watermark in Task 10 draws with. The runtime image
# ships no fonts at all.
RUN apt-get update \
 && apt-get install -y --no-install-recommends fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ROLE=web
EXPOSE 8080

# APP_UID is defined by the Microsoft base image (uid 1654). Running as
# root inside a container is the single most common finding in a
# container security review.
USER $APP_UID

ENTRYPOINT ["dotnet", "Garage.dll"]
