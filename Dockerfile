# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Create a volume for the data folder to persist data across container restarts
# /app/Data

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
RUN apk add --upgrade --no-cache tzdata
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
ARG NUGET_AUTH_TOKEN=token
ARG NUGET_URL=https://nuget.pkg.github.com/Revolutionized-IoT2/index.json
WORKDIR /src
COPY ["RIoT2.Elsa.Server/RIoT2.Elsa.Server.csproj", "RIoT2.Elsa.Server/"]
COPY ["RIoT2.Elsa.Studio/RIoT2.Elsa.Studio.csproj", "RIoT2.Elsa.Studio/"]
RUN dotnet nuget add source -n github -u AZ -p $NUGET_AUTH_TOKEN --store-password-in-clear-text $NUGET_URL
RUN dotnet restore "RIoT2.Elsa.Server/RIoT2.Elsa.Server.csproj"
COPY . .
WORKDIR "/src/RIoT2.Elsa.Server"
RUN dotnet build "RIoT2.Elsa.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "RIoT2.Elsa.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RIoT2.Elsa.Server.dll"]

#Set default environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV RIOT2_MQTT_IP=192.168.0.30
ENV RIOT2_MQTT_PASSWORD=password
ENV RIOT2_MQTT_USERNAME=user
ENV RIOT2_WORKFLOW_ID=E27E898E-82DB-42C9-AC58-E93413CE7266
ENV RIOT2_WORKFLOW_URL=http://192.168.0.32
ENV TZ=Europe/Helsinki