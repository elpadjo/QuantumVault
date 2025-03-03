# QuantumVault Architecture Overview

## **1. High-Level Design**  
QuantumVault is a **persistent Key/Value store** designed for efficiency, scalability, and crash resilience. It employs a combination of **in-memory storage, Write-Ahead Logging (WAL), and Sorted String Tables (SSTables)** to achieve **low-latency reads**, **high-throughput writes**, and **fast recovery** after crashes.

### **Core Components**
- **API Layer:** Exposes a RESTful interface for clients.
- **In-Memory Store:** Uses a `SortedDictionary` for fast key-value lookups.
- **Write-Ahead Log (WAL):** Ensures durability by logging operations before applying them.
- **SSTables:** Persistent storage for datasets exceeding RAM capacity, periodically compacted.
- **Background Services:** Handle request queuing, WAL flushing, and SSTable compaction.
- **Dynamic Throttling:** Adjusts batch sizes and processing rates based on system load.

---

## **2. Persistence Strategy**  
### **Storage Engine & Data Consistency**
- **Keys are stored in a case-insensitive manner** to prevent duplicate entries with different cases.
- **Internally, all keys are converted to lowercase before storage and retrieval.**
- **Values remain case-sensitive**, ensuring data integrity while maintaining a consistent lookup mechanism.
- This approach prevents inconsistencies like `("Key1", "ValueA")` and `("key1", "ValueB")` being treated as separate keys.

### **Write Path (Put/Bulk Put)**
1. Data is **stored in-memory** for quick access.  
2. The operation is **logged in the WAL** for durability.  
3. A background process periodically **flushes data to SSTables**, removing old WAL entries.  
4. **SSTables are compacted** periodically to optimize storage.  

### **Read Path (Get/Range Reads)**
1. Data is first searched in the **in-memory store**.  
2. If not found, SSTables are scanned **in sorted order** for efficiency.  
3. The most recent value is returned to the client.  

### **Delete Path**
1. The key is removed from **in-memory storage**.  
2. A **delete marker (tombstone)** is added to WAL.  
3. Background compaction permanently removes tombstoned keys from SSTables.  

---

## **3. Crash Recovery Process**
1. On restart, **WAL entries are replayed** to restore recent updates.  
2. Data from **SSTables is reloaded** into memory.  
3. The system resumes operations with minimal downtime.  

---

## **4. Performance Optimizations**  
### **Write Optimization**
- **Queued batch writes** improve throughput and reduce disk I/O.  
- **Load-based throttling** adjusts processing dynamically.  

### **Read Optimization**
- **In-memory caching** speeds up frequently accessed data.  
- **Efficient SSTable scans** maintain predictable read latency.  

### **Load Management**
- **Semaphore-based rate limiting** prevents overload.  
- **CPU/memory monitoring** dynamically adjusts batch sizes.  

---

## **5. Trade-offs & Scalability Considerations**  
### **Trade-offs**
✔ **Fast reads & writes** due to in-memory storage.  
✖ **Higher memory usage**, requiring periodic SSTable flushes.  
✔ **Crash safety with WAL & SSTables.**  
✖ **SSTable lookups may add slight overhead** for large datasets.  

### **Scalability Enhancements (Future Work)**
- **Bloom filters** to minimize unnecessary SSTable lookups.  
- **Sharding** for horizontal scalability.  
- **Distributed consensus (Raft/Paxos)** for multi-node replication.  

---

## **6. Deployment with Docker**
### **Containerized Setup**
QuantumVault is packaged as a **Docker container** for easy deployment.  
1. **Build & Run**
   ```sh
   docker-compose up --build -d
   ```
2. The service will be available at `http://localhost:8080/quantumvault/v1/`

---

## **7. Research References**  
- **Bigtable (Google)**: SSTable-based storage model for efficient persistence.  
- **Bitcask (Riak)**: WAL-based crash recovery and log-structured writes.  
- **LSM-Tree (LevelDB, RocksDB)**: Efficient storage management using compaction.  
- **Raft/Paxos**: Potential for future distributed consensus mechanisms.  

---

## **8. Conclusion**  
QuantumVault ensures **low-latency access, high throughput, and reliable persistence** while handling large datasets efficiently. By leveraging **WAL, SSTables, and in-memory caching**, it provides predictable performance under varying loads.



For more details, check the [API Documentation](API.md).

