Event-Driven Backend (.NET + Kafka)

This project is a simple backend application using .NET 8 and Apache Kafka.
It receives user activity events through an API, publishes them to Kafka, and processes them asynchronously using a background consumer.

All processed events are stored in a thread-safe in-memory list and can be retrieved using another API endpoint.

How it works

The API receives an event via POST /events.

The event is serialized to JSON and published to a Kafka topic (user-events).

A background Kafka consumer reads messages from that topic.

Each consumed event is deserialized and saved in memory.

GET /events returns the most recent processed events.

No database is used — data is kept in memory for simplicity.

Requirements

.NET 8 SDK

Docker (for Kafka)

Kafka broker running on localhost:9092

Configuration

Edit appsettings.json if needed:

{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "user-events",
    "GroupId": "challenge-consumer"
  }
}

Running Kafka locally

You can start Kafka using Docker Compose:

services:
  zookeeper:
    image: bitnami/zookeeper:3.9
    environment:
      - ALLOW_ANONYMOUS_LOGIN=yes
    ports: ["2181:2181"]

  kafka:
    image: bitnami/kafka:3.7
    ports: ["9092:9092"]
    environment:
      - KAFKA_CFG_NODE_ID=1
      - KAFKA_CFG_PROCESS_ROLES=broker,controller
      - KAFKA_CFG_CONTROLLER_LISTENER_NAMES=CONTROLLER
      - KAFKA_CFG_LISTENERS=PLAINTEXT://:9092,CONTROLLER://:9093
      - KAFKA_CFG_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092
      - KAFKA_CFG_CONTROLLER_QUORUM_VOTERS=1@localhost:9093
      - ALLOW_PLAINTEXT_LISTENER=yes


Start it with:

docker compose -f docker-compose.kafka.yml up -d

Running the API
dotnet run --project Challenge.Api


Swagger will be available at:
http://localhost:5000/swagger

Testing the API
Send events
curl -X POST http://localhost:5000/events \
  -H "Content-Type: application/json" \
  -d '{"userId":"u1","type":"login"}'

curl -X POST http://localhost:5000/events \
  -H "Content-Type: application/json" \
  -d '{"userId":"u2","type":"purchase","data":{"amount":99.9}}'


Expected response: 202 Accepted

Get processed events
curl -s http://localhost:5000/events?limit=10 | jq .


Example output:

[
  {
    "userId": "u1",
    "type": "login",
    "timestamp": "2025-11-06T18:00:00Z",
    "data": null
  }
]

Validation rules

userId and type are required.

type must be one of: login, logout, purchase.

timestamp is optional; if not provided, server time is used.

Invalid input returns 400 Bad Request.

Notes

The in-memory store is cleared on every restart.

It may take a few milliseconds for a new event to appear in GET /events, since processing is asynchronous.

The goal is to demonstrate an event-driven design, not to persist data permanently.