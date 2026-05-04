# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore DocVault.sln
RUN dotnet publish src/DocVault.Api/DocVault.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Install libgssapi-krb5-2 (Npgsql GSSAPI), tesseract-ocr + English language data (OCR)
RUN apt-get update -o Acquire::Retries=3 \
    && apt-get install -y --no-install-recommends \
         libgssapi-krb5-2 \
         curl \
         tesseract-ocr \
         tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "DocVault.Api.dll"]
