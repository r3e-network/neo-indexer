#!/usr/bin/env node
/**
 * Refresh Supabase schema cache and verify tables
 */

const SUPABASE_URL = 'https://pthxoebiwpmkczmbhdkf.supabase.co';
const SUPABASE_SERVICE_KEY = process.env.SUPABASE_SERVICE_ROLE_KEY;

async function main() {
  console.log('🔄 Refreshing Supabase schema cache...\n');

  // Call the PostgREST schema cache reload endpoint
  const reloadRes = await fetch(`${SUPABASE_URL}/rest/v1/`, {
    method: 'GET',
    headers: {
      'apikey': SUPABASE_SERVICE_KEY,
      'Authorization': `Bearer ${SUPABASE_SERVICE_KEY}`,
      'Accept': 'application/json',
      'Prefer': 'return=representation'
    }
  });

  console.log('Schema endpoint status:', reloadRes.status);

  // Test each table directly
  const tables = ['blocks', 'transactions', 'op_traces'];

  for (const table of tables) {
    const res = await fetch(`${SUPABASE_URL}/rest/v1/${table}?select=*&limit=1`, {
      headers: {
        'apikey': SUPABASE_SERVICE_KEY,
        'Authorization': `Bearer ${SUPABASE_SERVICE_KEY}`,
        'Accept': 'application/json'
      }
    });

    const status = res.status;
    const body = await res.text();

    if (status === 200) {
      console.log(`✅ ${table}: accessible`);
    } else {
      console.log(`❌ ${table}: ${status} - ${body.slice(0, 100)}`);
    }
  }

  // Try to notify PostgREST to reload schema (this is a workaround)
  console.log('\n📋 If tables show as inaccessible, you may need to:');
  console.log('   1. Go to Supabase Dashboard > Settings > API');
  console.log('   2. Click "Reload schema cache" button');
  console.log('   Or wait a few minutes for automatic refresh');
}

main().catch(console.error);
