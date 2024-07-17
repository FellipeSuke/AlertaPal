# Use uma imagem base do .NET 8.0 SDK para construir a aplicação
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copie os arquivos .csproj e restaure as dependências
COPY *.csproj ./
RUN dotnet restore

# Copie o restante dos arquivos e compile a aplicação
COPY . ./
RUN dotnet publish -c Release -o out

# Use uma imagem runtime do .NET 8.0 e instale o curl
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Defina o ponto de entrada da aplicação
ENTRYPOINT ["dotnet", "AlertaPal.dll"]
