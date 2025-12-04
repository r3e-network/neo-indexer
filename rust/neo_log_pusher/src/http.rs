use std::net::SocketAddr;
use std::sync::Arc;

use hyper::service::{make_service_fn, service_fn};
use hyper::{Body, Request, Response, Server};
use serde_json::json;
use tokio_postgres::Client;

use crate::metrics::Metrics;

pub async fn serve(
    addr: SocketAddr,
    client: Arc<Client>,
    metrics: Arc<Metrics>,
) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
    let make_svc = make_service_fn(move |_| {
        let client = client.clone();
        let metrics = metrics.clone();
        async move {
            Ok::<_, hyper::Error>(service_fn(move |req| {
                let client = client.clone();
                let metrics = metrics.clone();
                async move { handle(req, client, metrics).await }
            }))
        }
    });

    let server = Server::bind(&addr).serve(make_svc);
    server.await?;
    Ok(())
}

async fn handle(req: Request<Body>, client: Arc<Client>, metrics: Arc<Metrics>) -> Result<Response<Body>, hyper::Error> {
    match req.uri().path() {
        "/metrics" => {
            let db_ok = client.query_one("SELECT 1", &[]).await.is_ok();
            let mut out = String::new();
            out.push_str("# TYPE neo_log_pusher_db_ok gauge\n");
            out.push_str(&format!("neo_log_pusher_db_ok {}\n", if db_ok { 1 } else { 0 }));
            out.push_str("# TYPE neo_log_pusher_processed_files counter\n");
            out.push_str(&format!("neo_log_pusher_processed_files {}\n", metrics.processed_files()));
            if let Some(ts) = metrics.last_success() {
                out.push_str("# TYPE neo_log_pusher_last_success_timestamp gauge\n");
                out.push_str(&format!("neo_log_pusher_last_success_timestamp {}\n", ts));
            }
            if let Some(ts) = metrics.last_scan() {
                out.push_str("# TYPE neo_log_pusher_last_scan_timestamp gauge\n");
                out.push_str(&format!("neo_log_pusher_last_scan_timestamp {}\n", ts));
            }
            if let Some(err) = metrics.last_error() {
                out.push_str("# HELP neo_log_pusher_last_error Last recorded error message\n");
                out.push_str(&format!("neo_log_pusher_last_error{{message=\"{}\"}} 1\n", sanitize_label(err)));
            }
            return Ok(Response::builder()
                .status(200)
                .header("content-type", "text/plain; version=0.0.4")
                .body(Body::from(out))
                .unwrap());
        }
        _ => {
            let db_ok = client.query_one("SELECT 1", &[]).await.is_ok();
            let body = json!({
                "status": if db_ok { "ok" } else { "error" },
                "db_ok": db_ok,
                "processed_files": metrics.processed_files(),
                "last_success": metrics.last_success(),
                "last_scan": metrics.last_scan(),
                "last_error": metrics.last_error(),
            });

            let status = if db_ok { 200 } else { 500 };
            return Ok(Response::builder()
                .status(status)
                .header("content-type", "application/json")
                .body(Body::from(body.to_string()))
                .unwrap());
        }
    }
}

fn sanitize_label(msg: String) -> String {
    msg.replace('\\', "\\\\").replace('"', "\\\"")
}
