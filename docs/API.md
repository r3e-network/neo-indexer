# Neo Deep Trace API Documentation

## Overview

The Neo Deep Trace API provides endpoints for querying Neo N3 blockchain opcode traces, transactions, and blocks.

**Base URL:** `https://your-deployment.netlify.app/api`

## Authentication

All endpoints are publicly accessible (read-only). Rate limiting is applied per IP address.

## Rate Limits

| Endpoint | Limit | Window |
|----------|-------|--------|
| `/api/search` | 30 requests | 60 seconds |
| `/api/call-graph` | 20 requests | 60 seconds |
| Other endpoints | 60 requests | 60 seconds |

## Endpoints

### GET /api/stats

Returns current indexer statistics.

**Response:**
```json
{
  "latestBlock": {
    "index": 3402100,
    "timestamp": 1701705600,
    "tx_count": 5
  },
  "recentBlocks": [...],
  "ingestionLagSeconds": 15,
  "opTraces": 2400000000,
  "transactions": 15000000
}
```

### GET /api/search

Search for blocks, transactions, or senders.

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `q` | string | Yes | Search query (block index, tx hash prefix, or sender prefix) |

**Response:**
```json
{
  "blocks": [
    { "index": 123456, "hash": "0x...", "timestamp": 1701705600, "tx_count": 3 }
  ],
  "transactions": [
    { "hash": "0x...", "block_index": 123456, "sender": "0x..." }
  ],
  "senders": ["0x..."]
}
```

### GET /api/call-graph

Get contract call graph for a transaction.

**Parameters:**
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `tx` | string | Yes | Transaction hash (66 characters, 0x-prefixed) |

**Response:**
```json
{
  "nodes": [
    { "id": "0x...", "label": "0x1234ab...", "calls": 150, "gasUsed": 5000000 }
  ],
  "edges": [
    { "from": "entry", "to": "0x...", "label": "SYSCALL", "step": 42 }
  ],
  "stats": {
    "totalCalls": 500,
    "uniqueContracts": 3,
    "syscalls": [
      { "name": "System.Storage.Get", "count": 120 },
      { "name": "System.Contract.Call", "count": 45 }
    ]
  }
}
```

### GET /api/opcode-volume

Get opcode volume for recent blocks.

**Response:**
```json
{
  "points": [
    { "block_index": 3402100, "count": 15000 },
    { "block_index": 3402099, "count": 12500 }
  ]
}
```

### GET /api/live-opcodes

Get most recent opcode executions.

**Response:**
```json
{
  "rows": [
    {
      "block_index": 3402100,
      "tx_hash": "0x...",
      "step_order": 42,
      "opcode": "SYSCALL",
      "syscall": "System.Storage.Get",
      "contract_hash": "0x...",
      "gas_consumed": 1000
    }
  ]
}
```

### GET /api/health

Health check endpoint.

**Response:**
```json
{
  "status": "ok",
  "timestamp": 1701705600
}
```

## Error Responses

All endpoints return errors in the following format:

```json
{
  "error": "error_message"
}
```

**Common HTTP Status Codes:**
| Code | Description |
|------|-------------|
| 400 | Bad Request - Invalid parameters |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error |

## Caching

Responses include `Cache-Control` headers:
- Stats: 15 seconds
- Search: 10 seconds
- Call Graph: 30 seconds
- Volume/Live: 15 seconds

## Examples

### cURL

```bash
# Get stats
curl https://your-deployment.netlify.app/api/stats

# Search for a transaction
curl "https://your-deployment.netlify.app/api/search?q=0xabc123"

# Get call graph
curl "https://your-deployment.netlify.app/api/call-graph?tx=0x1234567890abcdef..."
```

### JavaScript

```javascript
// Fetch transaction call graph
const response = await fetch(`/api/call-graph?tx=${txHash}`);
const data = await response.json();
console.log(data.stats.totalCalls);
```

## Database Schema

### blocks
| Column | Type | Description |
|--------|------|-------------|
| index | INT | Block height (PK) |
| hash | CHAR(66) | Block hash |
| timestamp | BIGINT | Unix timestamp |
| tx_count | INT | Transaction count |

### transactions
| Column | Type | Description |
|--------|------|-------------|
| hash | CHAR(66) | Transaction hash (PK) |
| block_index | INT | Block height |
| sender | CHAR(42) | Sender address |
| sys_fee | BIGINT | System fee |
| net_fee | BIGINT | Network fee |

### op_traces (Partitioned)
| Column | Type | Description |
|--------|------|-------------|
| tx_hash | CHAR(66) | Transaction hash |
| block_index | INT | Block height (partition key) |
| step_order | INT | Execution step |
| contract_hash | CHAR(42) | Contract being executed |
| opcode | VARCHAR(32) | OpCode name |
| syscall | VARCHAR(100) | Syscall name (if SYSCALL) |
| gas_consumed | BIGINT | Cumulative gas |
| stack_top | TEXT | Top stack element |

**Primary Key:** `(block_index, tx_hash, step_order)`
