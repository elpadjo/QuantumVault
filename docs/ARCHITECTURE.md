# QuantumVault Architecture Overview

## 1. **High-Level Design**
QuantumVault is a **persistent Key/Value store** designed for efficiency, scalability, and crash resilience. It employs a combination of **in-memory storage, Write-Ahead Logging (WAL), and Sorted String Tables (SSTables)** to achieve **low-latency reads**, **high-throughput writes**, and **fast recovery** after crashes.

### **Core Components**
- **API Layer:** Exposes a RESTful interface for clients.
- **In-Memory Store:** Uses a `ConcurrentDictionary` for fast key-value lookups.
- **Write-Ahead Log (WAL):** Ensures durability by logging operations before applying them.
- **SSTables:** Persistent storage for large datasets beyond RAM capacity.
- **Background Processes:** Handles request queuing, WAL flushing, and SSTable compaction.

---

## 2. **Persistence Strategy**
### **Write Path (Put/Bulk Put)**
1. Data is **written to the in-memory store** for fast access.
2. The operation is **logged in the WAL** for durability.
3. A background process periodically **flushes data to SSTables**, removing old WAL entries.

### **Read Path (Get/Range Reads)**
1. Data is first searched in the **in-memory store**.
2. If not found, SSTables are scanned in reverse chronological order.
3. The latest value is returned to the client.

### **Delete Path**
1. The key is removed from **in-memory storage**.
2. A **delete marker (tombstone)** is written to the WAL.
3. Background compaction removes tombstoned keys from SSTables.

---

## 3. **Crash Recovery Process**
1. On restart, **WAL entries are replayed** to restore recent changes.
2. Data from **SSTables is loaded** into memory.
3. The system is ready for new requests with minimal downtime.

---

## 4. **Concurrency & Performance Optimizations**
### **1. Write Queues & Batching**
- Incoming writes are **queued and batch-processed** to reduce disk I/O.
- The system throttles writes under **high CPU/memory usage**.

### **2. Read Optimization**
- **In-memory caching** provides instant access to frequently used keys.
- **SSTables are structured in sorted order** for efficient range scans.

### **3. Load Balancing & Rate Limiting**
- Read/Write operations are controlled using **semaphores**.
- System monitors **CPU load and memory usage** to adjust batch sizes dynamically.

---

## 5. **Trade-offs & Scalability Considerations**
### **Trade-offs:**
? **Fast reads & writes** due to in-memory storage.  
?? **Higher memory usage** as keys must fit in RAM before SSTable flush.  
? **Crash safety with WAL & SSTables**.  
?? **SSTable lookup may slow down reads** for very large datasets.  

### **Scalability Enhancements (Future Work):**
- Implement **Bloom filters** to reduce unnecessary SSTable reads.
- Introduce **sharding** for horizontal scalability.
- Add **distributed consensus (Raft)** for high availability.

---

## 6. **Deployment with Docker**
### **Containerized Setup**
QuantumVault is packaged as a **Docker container** for easy deployment.
1. **Build & Run**
   ```sh
   docker build -t quantumvault .
   docker run -p 5000:5000 quantumvault
   ```
2. The service will be available at `http://localhost:5000/quantumvault/v1/`

---

## 7. **Conclusion**
QuantumVault is designed for **high performance, fault tolerance, and scalability**. By leveraging **WAL, SSTables, and efficient caching**, it ensures **low-latency access and reliable persistence** while handling large datasets effectively.

For more details, check the [API Documentation](api.md).

