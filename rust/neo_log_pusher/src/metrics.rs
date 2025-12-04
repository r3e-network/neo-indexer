use std::sync::{
    atomic::{AtomicI64, AtomicU64, Ordering},
    Arc, Mutex,
};
use std::time::{SystemTime, UNIX_EPOCH};

#[derive(Default)]
pub struct Metrics {
    processed_files: AtomicU64,
    last_success_ts: AtomicI64,
    last_scan_ts: AtomicI64,
    last_error: Mutex<Option<String>>,
}

impl Metrics {
    pub fn new() -> Arc<Self> {
        Arc::new(Self::default())
    }

    pub fn record_scan(&self) {
        self.last_scan_ts.store(now(), Ordering::Relaxed);
    }

    pub fn record_success(&self) {
        self.processed_files.fetch_add(1, Ordering::Relaxed);
        self.last_success_ts.store(now(), Ordering::Relaxed);
        let mut err = self.last_error.lock().unwrap();
        *err = None;
    }

    pub fn record_error(&self, msg: String) {
        let mut err = self.last_error.lock().unwrap();
        *err = Some(msg);
    }

    pub fn processed_files(&self) -> u64 {
        self.processed_files.load(Ordering::Relaxed)
    }

    pub fn last_success(&self) -> Option<i64> {
        as_opt(self.last_success_ts.load(Ordering::Relaxed))
    }

    pub fn last_scan(&self) -> Option<i64> {
        as_opt(self.last_scan_ts.load(Ordering::Relaxed))
    }

    pub fn last_error(&self) -> Option<String> {
        self.last_error.lock().unwrap().clone()
    }
}

fn now() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs() as i64
}

fn as_opt(v: i64) -> Option<i64> {
    if v == 0 { None } else { Some(v) }
}
