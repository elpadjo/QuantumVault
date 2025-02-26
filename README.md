# TheQuantumVault
### A .NET 8 Distributed Key/Value Store
This project is a high-performance, persistent Key/Value store built using .NET 8 with a focus on low latency, high throughput, and fault tolerance. It provides a RESTful API for storing and retrieving key-value pairs, supporting:

## Endpoints:
- PUT /store – Store a key-value pair
= GET /store/{key} – Retrieve a value by key
- DELETE /store/{key} – Remove a key-value pair
- GET /store/range?startKey=A&endKey=Z – Retrieve a range of keys
- POST /store/batch – Store multiple key-value pairs

## Features:
- Persistent Storage – Ensures data durability beyond application restarts
- Replication & Failover – Multi-node support with automatic failover
- Scalability – Handles large datasets efficiently
- Dockerized Deployment – Includes docker-compose.yml for multi-node setup

## Tech Stack:
- .NET 8 (Minimal APIs)
- File-based Persistent Storage
- Docker & Docker Compose for multi-node deployment

## Setup & Usage:
- Clone the repo
- Run docker-compose up -d to start multiple instances
- Use a REST client or curl to interact with the API

## Future Enhancements:
- Load balancing for better traffic distribution
- Improved data replication strategies

## License
Apache 2.0