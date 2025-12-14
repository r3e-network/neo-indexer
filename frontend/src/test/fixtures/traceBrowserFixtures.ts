import type {
  BlockTraceResult,
  ContractCallTraceEntry,
  OpCodeTraceEntry,
  SyscallTraceEntry,
  TransactionTraceResult,
} from '../../types';

export const sampleOpcodes: OpCodeTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xaaaa1111bbbb',
    instructionPointer: 0,
    opcode: 'PUSH1',
    opcodeName: 'PUSH1',
    operand: '0x01',
    gasConsumed: 2_00000000,
    stackDepth: 1,
    order: 1,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xbbbb2222cccc',
    instructionPointer: 4,
    opcode: 'SHA256',
    opcodeName: 'SHA256',
    operand: '0x02',
    gasConsumed: 5000000,
    stackDepth: 5,
    order: 2,
  },
];

export const sampleSyscalls: SyscallTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xaaaa1111bbbb',
    syscallName: 'System.Storage.Get',
    syscallHash: '0x01',
    gasCost: 4_00000000,
    order: 1,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    contractHash: '0xbbbb2222cccc',
    syscallName: 'System.Contract.Call',
    syscallHash: '0x02',
    gasCost: 10_00000000,
    order: 2,
  },
];

export const sampleContractCalls: ContractCallTraceEntry[] = [
  {
    blockIndex: 1,
    txHash: '0xtx1',
    callerHash: null,
    calleeHash: '0xaaaa1111bbbb',
    methodName: 'deploy',
    callDepth: 0,
    order: 1,
    gasConsumed: 10_00000000,
  },
  {
    blockIndex: 1,
    txHash: '0xtx1',
    callerHash: '0xaaaa1111bbbb',
    calleeHash: '0xbbbb2222cccc',
    methodName: 'invoke',
    callDepth: 1,
    order: 2,
    gasConsumed: 2_00000000,
  },
];

export const sampleTransaction: TransactionTraceResult = {
  txHash: '0xtx1',
  blockIndex: 777,
  opcodes: sampleOpcodes,
  syscalls: sampleSyscalls,
  contractCalls: sampleContractCalls,
};

export const blockTrace: BlockTraceResult = {
  blockIndex: 777,
  blockHash: '0xblock',
  transactions: [sampleTransaction],
};

