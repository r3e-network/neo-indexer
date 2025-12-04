use tokio_postgres::Client;

pub async fn check_health(client: &Client) -> Result<(), tokio_postgres::Error> {
    let row = client.query_one("SELECT 1", &[]).await?;
    let _: i32 = row.get(0);
    Ok(())
}
