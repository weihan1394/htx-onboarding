FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY *.csproj .
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet restore -r $RID

COPY . .
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet publish -c Release -r $RID --no-self-contained -o /app/publish

FROM cgr.dev/chainguard/aspnet-runtime:latest AS runtime
COPY --from=build /app/publish /app
WORKDIR /app

ENV ASPNETCORE_CONTENTROOT=/app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "OnboardingService.dll"]