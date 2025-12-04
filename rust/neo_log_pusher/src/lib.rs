// Library exports for testing
pub mod health;
pub mod http;
pub mod metrics;

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Arc;

    #[test]
    fn test_metrics_new() {
        let metrics = metrics::Metrics::new();
        assert_eq!(metrics.processed_files(), 0);
        assert!(metrics.last_success().is_none());
        assert!(metrics.last_scan().is_none());
        assert!(metrics.last_error().is_none());
    }

    #[test]
    fn test_metrics_record_success() {
        let metrics = metrics::Metrics::new();
        metrics.record_success();
        assert_eq!(metrics.processed_files(), 1);
        assert!(metrics.last_success().is_some());
    }

    #[test]
    fn test_metrics_record_scan() {
        let metrics = metrics::Metrics::new();
        metrics.record_scan();
        assert!(metrics.last_scan().is_some());
    }

    #[test]
    fn test_metrics_record_error() {
        let metrics = metrics::Metrics::new();
        metrics.record_error("test error".to_string());
        assert_eq!(metrics.last_error(), Some("test error".to_string()));
    }

    #[test]
    fn test_metrics_error_cleared_on_success() {
        let metrics = metrics::Metrics::new();
        metrics.record_error("test error".to_string());
        assert!(metrics.last_error().is_some());
        metrics.record_success();
        assert!(metrics.last_error().is_none());
    }

    #[test]
    fn test_metrics_multiple_successes() {
        let metrics = metrics::Metrics::new();
        for _ in 0..10 {
            metrics.record_success();
        }
        assert_eq!(metrics.processed_files(), 10);
    }
}
