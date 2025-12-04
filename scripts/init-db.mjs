#!/usr/bin/env node
/**
 * Initialize Supabase database schema
 * Run: node scripts/init-db.mjs
 */

import { createClient } from '@supabase/supabase-js';

const SUPABASE_URL = 'https://pthxoebiwpmkczmbhdkf.supabase.co';
const SUPABASE_SERVICE_KEY = process.env.SUPABASE_SERVICE_ROLE_KEY;

if (!SUPABASE_SERVICE_KEY) {
  console.error('Error: SUPABASE_SERVICE_ROLE_KEY environment variable is required');
  console.error('Get it from: Supabase Dashboard > Settings > API > service_role key');
  process.exit(1);
}

const supabase = createClient(SUPABASE_URL, SUPABASE_SERVICE_KEY, {
  db: { schema: 'public' },
  auth: { persistSession: false }
});

async function runSQL(sql, description) {
  console.log(`\n📦 ${description}...`);
  const { data, error } = await supabase.rpc('exec_sql', { sql_query: sql });
  if (error) {
    // Try direct query for simple statements
    const { error: err2 } = await supabase.from('_init').select('*').limit(0);
    if (err2?.code === '42P01') {
      // Table doesn't exist, which is expected
    }
    console.log(`   ⚠️  RPC not available, schema must be initialized via SQL Editor`);
    return false;
  }
  console.log(`   ✅ Done`);
  return true;
}

async function checkTables() {
  console.log('\n🔍 Checking existing tables...');

  const tables = ['blocks', 'transactions', 'op_traces'];
  const results = {};

  for (const table of tables) {
    const { count, error } = await supabase
      .from(table)
      .select('*', { count: 'exact', head: true });

    if (error) {
      results[table] = { exists: false, error: error.message };
    } else {
      results[table] = { exists: true, count };
    }
  }

  return results;
}

async function main() {
  console.log('🚀 Neo Indexer Database Initialization');
  console.log('=====================================');
  console.log(`Supabase URL: ${SUPABASE_URL}`);

  // Check current state
  const tableStatus = await checkTables();

  console.log('\n📊 Table Status:');
  for (const [table, status] of Object.entries(tableStatus)) {
    if (status.exists) {
      console.log(`   ✅ ${table}: exists (${status.count ?? 0} rows)`);
    } else {
      console.log(`   ❌ ${table}: ${status.error}`);
    }
  }

  const allExist = Object.values(tableStatus).every(s => s.exists);

  if (allExist) {
    console.log('\n✅ All tables already exist! Database is ready.');
    return;
  }

  console.log('\n⚠️  Some tables are missing.');
  console.log('\n📋 Please run the following SQL in Supabase SQL Editor:');
  console.log('   https://supabase.com/dashboard/project/pthxoebiwpmkczmbhdkf/sql/new');
  console.log('\n   1. Copy contents of: supabase/schema.sql');
  console.log('   2. Copy contents of: supabase/policies.sql');
  console.log('   3. Click "Run" for each file');
}

main().catch(console.error);
