# syntax=docker/dockerfile:1

# ---------- 1) Build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["E-Commerce.csproj", "./"]
RUN dotnet restore "E-Commerce.csproj"
COPY . .
RUN dotnet publish "E-Commerce.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- 2) Run ----------
# Debian-based image (NOT alpine): it includes ICU, which Khmer (km) culture needs.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .

# Folders the app writes to (uploaded covers, avatars, login keys); 1654 = the non-root "app" user
RUN mkdir -p /app/keys wwwroot/images/books wwwroot/images/avatars \
 && chown -R 1654:1654 /app/keys wwwroot/images
USER 1654

EXPOSE 8080
ENTRYPOINT ["dotnet", "E-Commerce.dll"]
