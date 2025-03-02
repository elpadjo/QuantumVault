# QuantumVault - Persistent Key/Value Store

QuantumVault is a high-performance, network-available persistent Key/Value store designed for efficiency, scalability, and crash resilience. It supports low-latency read/write operations, batch processing, and persistence using Write-Ahead Logging (WAL) and SSTables.

## Features
- **Persistent Storage**: Uses WAL and SSTables for data durability.
- **High Throughput**: Efficient batch processing and concurrency handling.
- **Crash Resilience**: Ensures fast recovery with WAL replay.
- **Scalable Reads/Writes**: In-memory caching and file-based persistence for large datasets.
- **RESTful API**: Exposes simple endpoints for data operations.
- **Dockerized Deployment**: Fully containerized using Docker for easy deployment.

## Installation & Setup
### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (If you don't want to use docker)
- [Docker](https://www.docker.com/get-started)

### Running with Docker
1. Clone the repository:
   ```sh
   git clone https://github.com/your-username/quantumvault.git
   cd quantumvault
   ```
2. Build and run the Docker container:
   ```sh
   docker-compose up --build -d
   ```
3. The service will be available at `http://localhost:8080/quantumvault/v1/`

## API Usage
### 1. Store a Key-Value Pair
   ```sh
   curl -X POST "http://localhost:8080/quantumvault/v1/put" \
        -H "Content-Type: application/json" \
        -d '{"Key": "username", "Value": "john_doe"}'
   ```

### 2. Retrieve a Value by Key
   ```sh
   curl -X GET "http://localhost:8080/quantumvault/v1/read/username"
   ```

### 3. Delete a Key
   ```sh
   curl -X DELETE "http://localhost:8080/quantumvault/v1/delete/username"
   ```

### 4. Batch Store Multiple Keys
   ```sh
   curl -X POST "http://localhost:8080/quantumvault/v1/batchput" \
        -H "Content-Type: application/json" \
        -d '{"KeyValues": {"key1": "value1", "key2": "value2"}}'
   ```

### 5. Read Keys in a Range
   ```sh
   curl -X GET "http://localhost:8080/quantumvault/v1/range?startKey=key1&endKey=key2"
   ```

## Running Tests
QuantumVault uses xUnit for testing.
```sh
cd QuantumVault.Tests
dotnet test
```

## More Documentation
For more details, check the [API Documentation](docs/API.md) and [Architecture Overview](docs/ARCHITECTURE.md)

## License
This project is licensed under the [Apache 2.0 License].

## Contact
For inquiries, open an issue or reach out via email at `josserayz@gmail.com`.

