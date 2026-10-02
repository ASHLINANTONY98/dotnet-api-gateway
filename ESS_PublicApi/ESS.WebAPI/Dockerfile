FROM mcr.microsoft.com/dotnet/aspnet:8.0

USER root

# Install certificate trust utilities
RUN apt-get update \
    && apt-get install -y ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Trust the PrivateApi public certificate
COPY ess-privateapi.crt /usr/local/share/ca-certificates/ess-privateapi.crt
RUN update-ca-certificates

WORKDIR /app

COPY out .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "ESS.WebAPI.dll"]