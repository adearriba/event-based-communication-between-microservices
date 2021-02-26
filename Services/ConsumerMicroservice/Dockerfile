FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /

COPY "EventBasedCommunicationMicroservices.sln" "EventBasedCommunicationMicroservices.sln"

COPY "Services/ConsumerMicroservice/ConsumerMicroservice.csproj" "Services/ConsumerMicroservice/ConsumerMicroservice.csproj"
COPY "Services/ProducerMicroservice/ProducerMicroservice.csproj" "Services/ProducerMicroservice/ProducerMicroservice.csproj"
COPY "Infrastructure/EventBusRabbitMQ/EventBusRabbitMQ.csproj" "Infrastructure/EventBusRabbitMQ/EventBusRabbitMQ.csproj"
COPY "Infrastructure/EventBus/EventBus.csproj" "Infrastructure/EventBus/EventBus.csproj"

COPY "docker-compose.dcproj" "docker-compose.dcproj"
COPY "NuGet.config" "NuGet.config"

RUN dotnet restore "EventBasedCommunicationMicroservices.sln"

COPY . .
WORKDIR /Services/ConsumerMicroservice
RUN dotnet publish --no-restore -c Release -o /app

FROM build AS publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "ConsumerMicroservice.dll"]