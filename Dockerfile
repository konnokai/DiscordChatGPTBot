#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["DiscordChatGPTBot/DiscordChatGPTBot.csproj", "DiscordChatGPTBot/"]
RUN dotnet restore "DiscordChatGPTBot/DiscordChatGPTBot.csproj"
COPY . .
WORKDIR "/src/DiscordChatGPTBot"
RUN dotnet build "DiscordChatGPTBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DiscordChatGPTBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
COPY --from=publish /app/publish .

ENV TZ="Asia/Taipei"

STOPSIGNAL SIGQUIT

ENTRYPOINT ["dotnet", "DiscordChatGPTBot.dll"]