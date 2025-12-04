#!/usr/bin/env node
/**
 * Initialize Supabase database schema via direct PostgreSQL connection
 */

import pg from 'pg';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Supabase connection - requires DATABASE_URL environment variable
// Format: postgres://postgres.[project-ref]:[password]@aws-0-[region].pooler.supabase.com:5432/postgres
const DATABASE_URL = process.env.DATABASE_URL;

if (!DATABASE_URL) {
  console.error('Error: DATABASE_URL environment variable is required');
  console.error('Set it in .env or pass it directly: DATABASE_URL=... node scripts/init-schema.mjs');
  process.exit(1);
}

async function main() {
  console.log('🚀 Neo Indexer Schema Initialization');
  console.log('====================================\n');

  const client = new pg.Client({
    connectionString: DATABASE_URL,
    ssl: { rejectUnauthorized: false }
  });

  try {
    console.log('📡 Connecting to Supabase PostgreSQL...');
    await client.connect();
    console.log('✅ Connected!\n');

    // Read schema file
    const schemaPath = path.join(__dirname, '..', 'supabase', 'schema.sql');
    const policiesPath = path.join(__dirname, '..', 'supabase', 'policies.sql');

    console.log('📦 Executing schema.sql...');
    const schemaSql = fs.readFileSync(schemaPath, 'utf8');

    // Split by semicolons but handle $$ blocks
    const statements = splitSqlStatements(schemaSql);

    for (const stmt of statements) {
      const trimmed = stmt.trim();
      if (!trimmed || trimmed.startsWith('--')) continue;

      try {
        await client.query(trimmed);
        // Show first 50 chars of each statement
        const preview = trimmed.replace(/\s+/g, ' ').slice(0, 60);
        console.log(`   ✓ ${preview}...`);
      } catch (err) {
        if (err.code === '42P07') {
          // Table already exists
          console.log(`   ⚠ Already exists: ${trimmed.slice(0, 40)}...`);
        } else if (err.code === '42710') {
          // Object already exists
          console.log(`   ⚠ Already exists: ${trimmed.slice(0, 40)}...`);
        } else {
          console.error(`   ❌ Error: ${err.message}`);
          console.error(`      Statement: ${trimmed.slice(0, 100)}...`);
        }
      }
    }

    console.log('\n📦 Executing policies.sql...');
    const policiesSql = fs.readFileSync(policiesPath, 'utf8');
    const policyStatements = splitSqlStatements(policiesSql);

    for (const stmt of policyStatements) {
      const trimmed = stmt.trim();
      if (!trimmed || trimmed.startsWith('--')) continue;

      try {
        await client.query(trimmed);
        const preview = trimmed.replace(/\s+/g, ' ').slice(0, 60);
        console.log(`   ✓ ${preview}...`);
      } catch (err) {
        if (err.code === '42710' || err.message.includes('already exists')) {
          console.log(`   ⚠ Already exists`);
        } else {
          console.error(`   ❌ Error: ${err.message}`);
        }
      }
    }

    // Verify tables
    console.log('\n🔍 Verifying tables...');
    const { rows } = await client.query(`
      SELECT table_name
      FROM information_schema.tables
      WHERE table_schema = 'public'
      AND table_type = 'BASE TABLE'
      ORDER BY table_name
    `);

    console.log('   Tables found:');
    for (const row of rows) {
      console.log(`   ✅ ${row.table_name}`);
    }

    // Check partitions
    const { rows: partitions } = await client.query(`
      SELECT tablename
      FROM pg_tables
      WHERE tablename LIKE 'op_traces_p%'
      ORDER BY tablename
    `);

    if (partitions.length > 0) {
      console.log('\n   Partitions:');
      for (const p of partitions) {
        console.log(`   ✅ ${p.tablename}`);
      }
    }

    console.log('\n✅ Schema initialization complete!');

  } catch (err) {
    console.error('❌ Connection error:', err.message);
    process.exit(1);
  } finally {
    await client.end();
  }
}

function splitSqlStatements(sql) {
  const statements = [];
  let current = '';
  let inDollarQuote = false;

  const lines = sql.split('\n');
  for (const line of lines) {
    // Check for $$ delimiter
    const dollarMatches = line.match(/\$\$/g);
    if (dollarMatches) {
      for (const _ of dollarMatches) {
        inDollarQuote = !inDollarQuote;
      }
    }

    current += line + '\n';

    // If we're not in a $$ block and line ends with ;
    if (!inDollarQuote && line.trim().endsWith(';')) {
      statements.push(current.trim());
      current = '';
    }
  }

  if (current.trim()) {
    statements.push(current.trim());
  }

  return statements;
}

main().catch(console.error);
