# QuantumVault - Persistent Key/Value Store

QuantumVault is a high-performance, network-available persistent Key/Value store designed for efficiency, scalability, and crash resilience. It supports low-latency read/write operations, batch processing, and persistence using Write-Ahead Logging (WAL) and SSTables.

## Features
- **Persistent Storage**: Implements Write-Ahead Logging (WAL) and SSTables to ensure data durability and crash resilience.
- **Low Latency Reads/Writes**: Uses an in-memory `SortedDictionary` for fast lookups, with semaphores for controlled read/write operations.
- **High Throughput**: Supports batching (`BatchPutAsync`), write queueing (`ConcurrentQueue`), and background services (`StorageBackgroundService`) for efficient processing.
- **Handling Large Datasets**: Periodic flushing of in-memory data to SSTables reduces memory overhead while maintaining quick access to frequently used data.
- **Crash Resilience**: WAL ensures data persistence, while snapshot recovery and journal replay mechanisms enable fast recovery after failures.
- **Predictable Performance**: Dynamic throttling, CPU-aware batch size adjustments, and priority-based request queuing help maintain stability under heavy load.
- **RESTful API**: Provides simple and intuitive endpoints for data operations, making integration straightforward.
- **Dockerized Deployment**: Fully containerized using Docker for easy deployment and scalability.

## Tradeoffs
- **Memory Usage vs. Persistence Speed**: Storing data in memory allows fast access but requires periodic flushing to disk to prevent excessive RAM usage.
- **Read Latency vs. Storage Efficiency**: Reads require checking both in-memory storage and SSTables, adding slight overhead compared to purely in-memory solutions.
- **Throughput vs. Durability**: Writes are batched and queued for efficiency, which may introduce slight delays before data is fully persisted to disk.

## Relation to Research Papers
- **Bigtable (Google)**: Inspired by Bigtable’s SSTable-based storage model, ensuring efficient reads/writes and scalable persistence.
- **Bitcask (Riak)**: Utilizes WAL and an append-only storage structure similar to Bitcask for crash recovery and efficient writes.
- **LSM-Tree (LevelDB, RocksDB)**: Implements a Log-Structured Merge-Tree approach with SSTable compaction for optimized storage management.
- **Raft/Paxos**: While not currently distributed, Raft/Paxos could be integrated for consensus and fault tolerance in a multi-node setup.



## Key-Value Storage Behavior
- **Keys are case-insensitive**: The system automatically converts all keys to lowercase before storing them.
- **Values are stored as-is**: Case sensitivity is preserved for values.
- **Example**:
  - Storing `("Key1", "Value1")` and `("key1", "Value2")` will overwrite, keeping only `"key1" → "Value2"`.
- **Range queries (`ReadKeyRangeAsync`) require valid keys.**
- **Both `startKey` and `endKey` must exist** before querying a range.
- **The range follows natural order**, meaning `endKey` must not be smaller than `startKey`.



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

