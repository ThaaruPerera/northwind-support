import { describe, expect, it } from 'vitest';
import {
  describeAgentChange,
  escalationBlockedReason,
  nextPriority,
  validateEscalation,
} from './escalation';

const sara = { id: 4, fullName: 'Sara Lindqvist' };
const ben = { id: 2, fullName: 'Ben Osei' };

describe('validateEscalation', () => {
  it.each([5, 6, 499, 500])('accepts a reason of %i characters', (length) => {
    expect(validateEscalation('a'.repeat(length))).toEqual({});
  });

  it.each([1, 4])('rejects a reason of %i characters as too short', (length) => {
    expect(validateEscalation('a'.repeat(length)).reason).toMatch(/at least 5/);
  });

  it('rejects a reason of 501 characters as too long', () => {
    expect(validateEscalation('a'.repeat(501)).reason).toMatch(/at most 500/);
  });

  it('judges the reason on its trimmed text', () => {
    expect(validateEscalation('   abc   ').reason).toMatch(/at least 5/);
    expect(validateEscalation(`  ${'a'.repeat(500)}  `)).toEqual({});
  });

  it.each(['', '      '])('asks for a reason when it is %j', (reason) => {
    expect(validateEscalation(reason).reason).toBe('A reason is required.');
  });

  it('has nothing to say about who is escalating, because the API takes that from the token', () => {
    expect(Object.keys(validateEscalation(''))).toEqual(['reason']);
  });
});

describe('nextPriority', () => {
  it.each([
    ['Low', 'Medium'],
    ['Medium', 'High'],
    ['High', 'Critical'],
  ] as const)('%s goes up to %s', (from, to) => {
    expect(nextPriority(from)).toBe(to);
  });

  it('has nowhere to go from Critical', () => {
    expect(nextPriority('Critical')).toBeUndefined();
  });
});

describe('escalationBlockedReason', () => {
  const open = { reference: 'TCK-0012', priority: 'High', status: 'Open' } as const;

  it('lets an open ticket below Critical through', () => {
    expect(escalationBlockedReason(open)).toBeUndefined();
  });

  it('says a Critical ticket cannot go higher', () => {
    expect(escalationBlockedReason({ ...open, priority: 'Critical' })).toBe(
      'Ticket TCK-0012 is already Critical and cannot be escalated.',
    );
  });

  it.each(['Resolved', 'Closed'] as const)('says a %s ticket cannot be escalated', (status) => {
    expect(escalationBlockedReason({ ...open, status })).toBe(
      `Ticket TCK-0012 is ${status} and cannot be escalated.`,
    );
  });

  it('names the finished status first, as the API does', () => {
    expect(escalationBlockedReason({ ...open, priority: 'Critical', status: 'Closed' })).toMatch(
      /is Closed/,
    );
  });
});

describe('describeAgentChange', () => {
  it('shows a change of agent', () => {
    expect(describeAgentChange(sara, ben)).toBe('Sara Lindqvist → Ben Osei');
  });

  it('says when the agent stayed', () => {
    expect(describeAgentChange(sara, sara)).toBe('Sara Lindqvist (unchanged)');
  });

  it('shows a ticket that was unassigned getting an agent', () => {
    expect(describeAgentChange(null, ben)).toBe('Unassigned → Ben Osei');
  });

  it('shows a ticket losing its agent', () => {
    expect(describeAgentChange(sara, null)).toBe('Sara Lindqvist → Unassigned');
  });

  it('says a ticket with no agent before or after is unassigned', () => {
    expect(describeAgentChange(null, null)).toBe('Unassigned');
  });
});
