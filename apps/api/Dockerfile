# syntax=docker/dockerfile:1
#
# Builds the API image. Build it from the repository root, so that the shared build files are in reach:
#
#   docker build -f apps/api/Dockerfile -t northwind-api .
#
# Nothing in here is configuration or a secret. The connection string, the JWT signing key and the
# environment name are all given when the container runs, and no environment name is chosen here, so a
# container that is not told one starts as Production.

# ---------------------------------------------------------------- restore
# Only the files that decide which packages are needed are copied in. A change to the source leaves this
# layer, and the slow package download it holds, cached.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/

# Restored and published for the platform this image is built on, so the native libraries of every other
# platform (Windows, macOS, the other CPU) are left out. BuildKit fills in TARGETARCH. This is not a
# multi-architecture build: each machine builds an image for itself.
ARG TARGETARCH
RUN case "$TARGETARCH" in \
      amd64) RID=linux-x64 ;; \
      arm64) RID=linux-arm64 ;; \
      *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && echo "$RID" > /tmp/rid \
    && dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj --runtime "$RID"

# ---------------------------------------------------------------- publish
FROM restore AS publish

COPY apps/api/src apps/api/src

# The development settings file is not copied here (.dockerignore) and not published (the project file).
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release --no-restore --runtime "$(cat /tmp/rid)" --self-contained false \
    --output /app/publish /p:UseAppHost=false /p:DebugType=None

# ---------------------------------------------------------------- runtime
# The ASP.NET runtime only: no SDK, no compiler, no source.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

# The image's own non-root user. The API never needs to be root, so it is not.
USER $APP_UID

# Plain HTTP on 8080, which a non-root user may bind. There are no certificates in the image; anything that
# needs HTTPS puts a proxy in front.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
