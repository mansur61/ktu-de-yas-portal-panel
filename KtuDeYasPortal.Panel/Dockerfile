# Build context: /Users/mansureminkaya/Desktop/Projects
# Panel, ktu-de-yas-shared içindeki Contracts ve Realtime projelerine referans verir.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /projects

COPY ktu-de-yas-shared/ ktu-de-yas-shared/
COPY ktu-de-yas-portal-panel/KtuDeYasPortal.Panel/ \
     ktu-de-yas-portal-panel/KtuDeYasPortal.Panel/

RUN dotnet publish ktu-de-yas-portal-panel/KtuDeYasPortal.Panel/KtuDeYasPortal.Panel.csproj \
    -c Release -o /app/publish --disable-parallel

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5056
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KtuDeYasPortal.Panel.dll"]
