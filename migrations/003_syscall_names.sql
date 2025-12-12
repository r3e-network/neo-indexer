-- Migration: 003_syscall_names.sql
-- Description: Seed syscall_names reference table with all built-in Neo syscalls.
-- Date: 2025-12-12
--
-- Values are sourced from ApplicationEngine.Services (hash, name, fixed price).

INSERT INTO syscall_names (hash, name, category, gas_base)
VALUES
    ('525B7D62', 'System.Contract.Call', 'Contract', 32768),
    ('677BF71A', 'System.Contract.CallNative', 'Contract', 0),
    ('09E9336A', 'System.Contract.CreateMultisigAccount', 'Contract', 0),
    ('028799CF', 'System.Contract.CreateStandardAccount', 'Contract', 0),
    ('813ADA95', 'System.Contract.GetCallFlags', 'Contract', 1024),
    ('93BCDB2E', 'System.Contract.NativeOnPersist', 'Contract', 0),
    ('165DA144', 'System.Contract.NativePostPersist', 'Contract', 0),
    ('3ADCD09E', 'System.Crypto.CheckMultisig', 'Crypto', 0),
    ('27B3E756', 'System.Crypto.CheckSig', 'Crypto', 32768),
    ('9CED089C', 'System.Iterator.Next', 'Iterator', 32768),
    ('1DBF54F3', 'System.Iterator.Value', 'Iterator', 16),
    ('BC8C5AC3', 'System.Runtime.BurnGas', 'Runtime', 16),
    ('8CEC27F8', 'System.Runtime.CheckWitness', 'Runtime', 1024),
    ('8B18F1AC', 'System.Runtime.CurrentSigners', 'Runtime', 16),
    ('CED88814', 'System.Runtime.GasLeft', 'Runtime', 16),
    ('DC92494C', 'System.Runtime.GetAddressVersion', 'Runtime', 8),
    ('3C6E5339', 'System.Runtime.GetCallingScriptHash', 'Runtime', 16),
    ('38E2B4F9', 'System.Runtime.GetEntryScriptHash', 'Runtime', 16),
    ('74A8FEDB', 'System.Runtime.GetExecutingScriptHash', 'Runtime', 16),
    ('43112784', 'System.Runtime.GetInvocationCounter', 'Runtime', 16),
    ('E0A0FBC5', 'System.Runtime.GetNetwork', 'Runtime', 8),
    ('F1354327', 'System.Runtime.GetNotifications', 'Runtime', 4096),
    ('28A9DE6B', 'System.Runtime.GetRandom', 'Runtime', 0),
    ('3008512D', 'System.Runtime.GetScriptContainer', 'Runtime', 8),
    ('0388C3B7', 'System.Runtime.GetTime', 'Runtime', 8),
    ('A0387DE9', 'System.Runtime.GetTrigger', 'Runtime', 8),
    ('8F800CB3', 'System.Runtime.LoadScript', 'Runtime', 32768),
    ('9647E7CF', 'System.Runtime.Log', 'Runtime', 32768),
    ('616F0195', 'System.Runtime.Notify', 'Runtime', 32768),
    ('F6FC79B2', 'System.Runtime.Platform', 'Runtime', 8),
    ('E9BF4C76', 'System.Storage.AsReadOnly', 'Storage', 16),
    ('EDC5582F', 'System.Storage.Delete', 'Storage', 32768),
    ('9AB830DF', 'System.Storage.Find', 'Storage', 32768),
    ('31E85D92', 'System.Storage.Get', 'Storage', 32768),
    ('CE67F69B', 'System.Storage.GetContext', 'Storage', 16),
    ('E26BB4F6', 'System.Storage.GetReadOnlyContext', 'Storage', 16),
    ('84183FE6', 'System.Storage.Put', 'Storage', 32768)
ON CONFLICT (hash) DO UPDATE
SET
    name = EXCLUDED.name,
    category = EXCLUDED.category,
    gas_base = EXCLUDED.gas_base;

