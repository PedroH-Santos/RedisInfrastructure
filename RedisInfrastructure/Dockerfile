# # Estágio de build com o SDK do .NET 10
# FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# WORKDIR /src

# # Copia o arquivo .csproj respeitando a estrutura de pastas e restaura as dependências
# COPY CacheApi/CacheApi.csproj ./CacheApi/
# RUN dotnet restore ./CacheApi/CacheApi.csproj

# # Copia todo o código fonte para o container
# COPY . .
# WORKDIR /src/CacheApi

# # Publica os binários compilados em modo Release
# RUN dotnet publish -c Release -o /app/publish

# # Estágio final utilizando o runtime do .NET 10
# FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# WORKDIR /app
# RUN mkdir -p /app/data

# # Copia o resultado do publish para o container final
# COPY --from=build /app/publish .

# # Configurações de ambiente do ASP.NET Core
# ENV ASPNETCORE_URLS=http://+:8080
# ENV ASPNETCORE_ENVIRONMENT=Production
# EXPOSE 8080

# # Ponto de entrada corrigido com o nome correto da DLL
# ENTRYPOINT ["dotnet", "CacheApi.dll"]