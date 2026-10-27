import { describe, expect, it } from 'vitest';
import { defaultReturnPath, readReturnPath, toReturnLocation } from './returnPath';

const from = (pathname: unknown, search: unknown = '', hash: unknown = '') => ({
  from: { pathname, search, hash },
});

describe('readReturnPath', () => {
  it('goes back to the page that was wanted, with its query and hash', () => {
    expect(readReturnPath(from('/tickets/12', '?tab=history', '#top'))).toBe('/tickets/12?tab=history#top');
  });

  it('goes back to a page with no query or hash', () => {
    expect(readReturnPath(from('/customers/3'))).toBe('/customers/3');
  });

  it.each([
    ['nothing at all', undefined],
    ['null', null],
    ['a string', 'tickets'],
    ['an object with no from', {}],
    ['a from that is not an object', { from: '/tickets' }],
    ['a from with no pathname', { from: { search: '?a=1' } }],
    ['a pathname that is not text', from(42)],
  ])('falls back to the ticket list for %s', (_name, state) => {
    expect(readReturnPath(state)).toBe(defaultReturnPath);
  });

  it.each([
    ['another site', 'https://evil.example/steal'],
    ['a protocol relative address', '//evil.example'],
    ['a relative path', 'tickets/12'],
    ['the login page itself', '/login'],
    ['the bare root', '/'],
  ])('refuses %s and falls back to the ticket list', (_name, pathname) => {
    expect(readReturnPath(from(pathname))).toBe(defaultReturnPath);
  });

  it('ignores a query or hash that is not text', () => {
    expect(readReturnPath(from('/tickets', 5, {}))).toBe('/tickets');
  });
});

describe('toReturnLocation', () => {
  it('keeps only the path, query and hash of a router location', () => {
    const location = { pathname: '/tickets', search: '?a=1', hash: '', state: { big: 'object' }, key: 'k' };

    expect(toReturnLocation(location)).toEqual({ pathname: '/tickets', search: '?a=1', hash: '' });
  });
});
