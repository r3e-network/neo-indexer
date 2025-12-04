use dotenv::dotenv;
use futures::{sink::SinkExt, TryStreamExt};
use std::env;
use std::path::{Path, PathBuf};
use std::str::FromStr;
use std::time::{Duration, SystemTime};
use tokio::fs::File;
use tokio::time;
use tokio_postgres::{Client, NoTls};
use tokio_util::codec::{BytesCodec, FramedRead};

mod health;
mod http;
mod metrics;

const DEFAULT_LOG_DIR: &str = "/neo-data/logs";
const DEFAULT_HTTP_PORT: &str = "8080";

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    dotenv().ok();
    let db_url = env::var("DATABASE_URL")?;
    let log_dir = env::var("LOG_DIR").unwrap_or_else(|_| DEFAULT_LOG_DIR.to_string());
    let http_port = env::var("HTTP_PORT").unwrap_or_else(|_| DEFAULT_HTTP_PORT.to_string());

    println!("🦀 Rust Log Pusher Active");

    let (client, connection) = tokio_postgres::connect(&db_url, NoTls).await?;
    tokio::spawn(async move {
        if let Err(e) = connection.await {
            eprintln!("DB connection error: {e}");
        }
    });
    let client = std::sync::Arc::new(client);
    let metrics = metrics::Metrics::new();

    // HTTP health server
    {
        let client = client.clone();
        let metrics = metrics.clone();
        let addr = std::net::SocketAddr::from_str(&format!("0.0.0.0:{}", http_port))?;
        tokio::spawn(async move {
            if let Err(e) = http::serve(addr, client, metrics).await {
                eprintln!("http server error: {e}");
            }
        });
    }

    loop {
        metrics.record_scan();
        if let Err(e) = process_dir(&log_dir, client.clone(), metrics.clone()).await {
            eprintln!("scan error: {e}");
            metrics.record_error(format!("scan error: {e}"));
        }
        if let Err(e) = health::check_health(&client).await {
            eprintln!("health check failed: {e}");
            metrics.record_error(format!("health error: {e}"));
        }
        time::sleep(Duration::from_secs(5)).await;
    }
}

async fn process_dir(
    dir: &str,
    client: std::sync::Arc<Client>,
    metrics: std::sync::Arc<metrics::Metrics>,
) -> Result<(), Box<dyn std::error::Error>> {
    let mut entries = tokio::fs::read_dir(dir).await?;
    while let Some(entry) = entries.next_entry().await? {
        let path = entry.path();
        let name = path.file_name().unwrap().to_string_lossy().to_string();

        if name.ends_with(".csv") && is_stable(&path).await? {
            match name.as_str() {
                n if n.starts_with("trace_") => {
                    println!("Uploading trace: {n}");
                    let cols = &["tx_hash", "block_index", "step_order", "contract_hash", "opcode", "syscall", "gas_consumed", "stack_top"];
                    upload(&client, &path, "op_traces", cols).await?;
                    metrics.record_success();
                }
                n if n.starts_with("blocks_") => {
                    upload(&client, &path, "blocks", &["index", "hash", "timestamp", "tx_count"]).await?;
                    metrics.record_success();
                }
                n if n.starts_with("txs_") => {
                    upload(&client, &path, "transactions", &["hash", "block_index", "sender", "sys_fee", "net_fee"]).await?;
                    metrics.record_success();
                }
                _ => {}
            }
        }
    }
    Ok(())
}

async fn is_stable(path: &Path) -> Result<bool, std::io::Error> {
    let meta = tokio::fs::metadata(path).await?;
    let modified = meta.modified()?;
    Ok(SystemTime::now().duration_since(modified).unwrap_or_default().as_secs() > 10)
}

const MAX_RETRIES: u32 = 3;
const RETRY_DELAY_MS: u64 = 1000;

async fn upload(client: &Client, path: &PathBuf, table: &str, cols: &[&str]) -> Result<(), Box<dyn std::error::Error>> {
    let mut last_error: Option<Box<dyn std::error::Error>> = None;

    for attempt in 1..=MAX_RETRIES {
        match upload_once(client, path, table, cols).await {
            Ok(()) => {
                // Success: remove the file
                if let Err(e) = tokio::fs::remove_file(path).await {
                    eprintln!("Warning: failed to remove {}: {}", path.display(), e);
                }
                return Ok(());
            }
            Err(e) => {
                eprintln!(
                    "Upload attempt {}/{} failed for {}: {}",
                    attempt, MAX_RETRIES, path.display(), e
                );
                last_error = Some(e);
                if attempt < MAX_RETRIES {
                    time::sleep(Duration::from_millis(RETRY_DELAY_MS * attempt as u64)).await;
                }
            }
        }
    }

    Err(last_error.unwrap_or_else(|| "Unknown upload error".into()))
}

async fn upload_once(client: &Client, path: &PathBuf, table: &str, cols: &[&str]) -> Result<(), Box<dyn std::error::Error>> {
    let file = File::open(path).await?;
    let stream = FramedRead::new(file, BytesCodec::new()).map_ok(|b| b.freeze());
    let query = format!(
        "COPY {} ({}) FROM STDIN WITH (FORMAT csv)",
        table,
        cols.join(",")
    );

    let sink = client.copy_in(&query).await?;
    tokio::pin!(stream);
    tokio::pin!(sink);

    while let Some(chunk) = stream.try_next().await? {
        sink.as_mut().send(chunk).await?;
    }
    sink.finish().await?;

    Ok(())
}
