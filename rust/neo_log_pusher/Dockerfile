# Builder
FROM rust:1.78-bookworm AS builder
WORKDIR /app
COPY Cargo.toml Cargo.lock* ./
RUN mkdir -p src && echo "// placeholder" > src/main.rs
RUN cargo build --release || true
COPY src ./src
RUN cargo build --release

# Runtime
FROM debian:bookworm-slim
WORKDIR /app
RUN apt-get update && apt-get install -y ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=builder /app/target/release/neo_log_pusher /app/neo_log_pusher
ENV LOG_DIR=/neo-data/logs
RUN mkdir -p /neo-data/logs
CMD ["./neo_log_pusher"]
