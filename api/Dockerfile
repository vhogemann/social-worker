FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SocialWorker.Api/*.csproj ./SocialWorker.Api/
WORKDIR /src/SocialWorker.Api
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet tool install --global dotnet-ef || true
ENV PATH="$PATH:/root/.dotnet/tools"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore
COPY SocialWorker.Api/ ./
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
	&& apt-get install -y --no-install-recommends libgssapi-krb5-2 \
	&& rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "SocialWorker.Api.dll"]
