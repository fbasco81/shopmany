#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.3 AS base
WORKDIR /app
EXPOSE 80
RUN echo here
RUN echo ciaox
WORKDIR /
RUN curl -LO https://github.com/DataDog/dd-trace-dotnet/releases/download/v1.16.0/datadog-dotnet-apm_1.16.0_amd64.deb
RUN dpkg -i ./datadog-dotnet-apm_1.16.0_amd64.deb
RUN echo ciao2
RUN ls /
RUN ls /opt
RUN ls /opt/datadog

FROM mcr.microsoft.com/dotnet/core/sdk:3.1.201 AS build
WORKDIR /src
COPY ["Account/Account.csproj", "Account/"]
RUN dotnet restore "Account/Account.csproj"
COPY . .
WORKDIR "/src/Account"
RUN dotnet build "Account.csproj" -c Release -o /app/build
RUN echo there
FROM build AS publish
RUN dotnet publish "Account.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Account.dll"]