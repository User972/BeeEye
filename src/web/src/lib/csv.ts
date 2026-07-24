/**
 * CSV export (V3-PLAT-007).
 *
 * Two separate hazards are handled here, and conflating them is how one of them gets missed:
 *
 * 1. **RFC-4180 quoting.** Commas, quotes and newlines inside a value must not break the row.
 * 2. **Formula injection.** A value beginning `=`, `+`, `-`, `@`, tab or carriage return is
 *    executed as a formula when the file is opened in Excel or Sheets. A decision log is exactly
 *    the kind of file someone opens in Excel, and a subject reference is attacker-influenced text
 *    the moment BeeEye ingests a real product catalogue.
 */

/** Characters a spreadsheet treats as the start of a formula. */
const FORMULA_PREFIXES = ['=', '+', '-', '@', '\t', '\r'];

/**
 * Escapes one CSV field.
 *
 * A dangerous leading character is neutralised with a single quote — the convention spreadsheets
 * understand as "this is text" — rather than stripped, so the value a user sees still matches the
 * value the platform holds.
 */
export function escapeCsvField(value: string | number | boolean | null | undefined): string {
  if (value === null || value === undefined) {
    return '';
  }

  let text = String(value);

  if (text.length > 0 && FORMULA_PREFIXES.includes(text.charAt(0))) {
    text = `'${text}`;
  }

  // Quoted whenever a delimiter, a quote or a line break appears; embedded quotes are doubled.
  if (/[",\r\n]/.test(text)) {
    return `"${text.replaceAll('"', '""')}"`;
  }

  return text;
}

export type CsvValue = string | number | boolean | null | undefined;

/** Joins one row of fields, escaping each. */
export function toCsvRow(values: readonly CsvValue[]): string {
  return values.map(escapeCsvField).join(',');
}

/**
 * Renders a full CSV document with a header row.
 *
 * CRLF line endings per RFC-4180, and a UTF-8 BOM is added by {@link downloadCsv} rather than
 * here, so the string stays a clean CSV for tests and for any other consumer.
 */
export function toCsv<T>(
  columns: readonly { header: string; value: (row: T) => CsvValue }[],
  rows: readonly T[],
): string {
  const lines = [toCsvRow(columns.map((c) => c.header))];
  for (const row of rows) {
    lines.push(toCsvRow(columns.map((c) => c.value(row))));
  }
  return lines.join('\r\n');
}

/**
 * Offers a CSV string to the browser as a download.
 *
 * Prefixed with a UTF-8 BOM: without it Excel on Windows reads the file in the system codepage and
 * mangles every non-ASCII character — which in this dataset includes the Arabic location names and
 * the "→" in a transfer recommendation.
 */
export function downloadCsv(filename: string, csv: string): void {
  // Escaped rather than written literally: an invisible U+FEFF in source is the kind of thing a
  // later editor deletes by accident and nobody notices until Excel mangles a file.
  const blob = new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);

  URL.revokeObjectURL(url);
}
