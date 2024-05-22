FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY bin/Release/net8.0/ .
RUN apt update && apt upgrade -y && apt install -y curl
RUN addgroup --system --gid 1000 rumblegroup && adduser --system --uid 1000 --ingroup rumblegroup --shell /bin/sh rumbleuser
RUN chown -R rumbleuser:rumblegroup /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER 1000
ENTRYPOINT ["dotnet", "chat-service.dll"]
