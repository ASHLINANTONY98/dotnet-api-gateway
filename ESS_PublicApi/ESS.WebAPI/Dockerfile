# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Set working directory
WORKDIR /app

# Copy published output
COPY out .

# Expose port
EXPOSE 80

# Run application
ENTRYPOINT ["dotnet", "ESS.WebAPI.dll"]