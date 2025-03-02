# QuantumVault API Documentation

## Base URL
```
http://localhost:8080/quantumvault/v1/
```

## Endpoints

### 1. **Store a Key-Value Pair**
- **Endpoint:** `POST /put`
- **Description:** Stores a key-value pair.
- **Request Body:**
  ```json
  {
    "Key": "username",
    "Value": "john_doe"
  }
  ```
- **Response:**
  ```json
  {
    "message": "Key stored successfully"
  }
  ```
- **Error Responses:**
  - `400 Bad Request` if the key or value is missing.

---

### 2. **Retrieve a Value by Key**
- **Endpoint:** `GET /read/{key}`
- **Description:** Fetches the value associated with a given key.
- **Response:**
  ```json
  {
    "key": "username",
    "value": "john_doe"
  }
  ```
- **Error Responses:**
  - `404 Not Found` if the key does not exist.

---

### 3. **Delete a Key**
- **Endpoint:** `DELETE /delete/{key}`
- **Description:** Deletes a key from the store.
- **Response:**
  ```json
  {
    "message": "Key deleted successfully"
  }
  ```
- **Error Responses:**
  - `404 Not Found` if the key does not exist.

---

### 4. **Batch Store Multiple Keys**
- **Endpoint:** `POST /batchput`
- **Description:** Stores multiple key-value pairs in batch.
- **Request Body:**
  ```json
  {
    "KeyValues": {
      "key1": "value1",
      "key2": "value2"
    }
  }
  ```
- **Response:**
  ```json
  {
    "key1": "value1",
    "key2": "value2"
  }
  ```
- **Error Responses:**
  - `400 Bad Request` if the request is empty.

---

### 5. **Read Keys in a Range**
- **Endpoint:** `GET /range?startKey={startKey}&endKey={endKey}&pageSize={pageSize}&pageNumber={pageNumber}`
- **Description:** Fetches all key-value pairs in the given key range with pagination.
- **Response:**
  ```json
  {
    "PageNumber": 1,
    "PageSize": 20,
    "TotalItems": 2,
    "Data": {
      "key1": "value1",
      "key2": "value2"
    }
  }
  ```
- **Error Responses:**
  - `404 Not Found` if no keys are found in range.

---

## Error Handling
| Status Code | Meaning |
|------------|---------|
| 400 Bad Request | Invalid input data |
| 404 Not Found | Key not found |
| 500 Internal Server Error | Unexpected failure |

## Authentication & Security
Currently, QuantumVault does not enforce authentication. Future versions may include API key-based authentication for better security.

## Contact
For support, open an issue on GitHub or contact `josserayz@gmail.com`.

