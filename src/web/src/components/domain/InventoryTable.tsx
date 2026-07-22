import { useEffect, useState } from 'react';
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
  type SortingState,
} from '@tanstack/react-table';
import type { InventoryItemRow } from '@/lib/api/inventory';
import { fmtInt, fmtNum, fmtSar, riskClass } from '@/lib/format';

const helper = createColumnHelper<InventoryItemRow>();
const numericColumns = new Set(['age', 'cover', 'risk', 'value']);

const columns = [
  helper.accessor('stockId', { header: 'Stock', enableSorting: false }),
  helper.display({ id: 'config', header: 'Config', cell: (c) => `${c.row.original.model} · ${c.row.original.variant}` }),
  helper.accessor('location', { header: 'Location', enableSorting: false }),
  helper.accessor('inventoryAgeDays', { id: 'age', header: 'Age (d)', cell: (c) => fmtInt(c.getValue()) }),
  helper.accessor('agingBand', { header: 'Aging', enableSorting: false }),
  helper.accessor('stockCover', {
    id: 'cover',
    header: 'Cover (mo)',
    cell: (c) => (c.getValue() >= 999 ? '∞' : fmtNum(c.getValue(), 1)),
  }),
  helper.accessor('trendDirection', { header: 'Trend', enableSorting: false }),
  helper.accessor('riskScore', {
    id: 'risk',
    header: 'Risk',
    cell: (c) => (
      <span className={`badge ${riskClass(c.row.original.riskBand)}`}>{c.getValue()}</span>
    ),
  }),
  helper.accessor('purchasePrice', { id: 'value', header: 'Value', cell: (c) => fmtSar(c.getValue()) }),
  helper.accessor('recommendedAction', { header: 'Recommended action', enableSorting: false }),
];

interface InventoryTableProps {
  rows: InventoryItemRow[];
  onSelect: (stockId: string) => void;
  onSortChange: (sortKey: string) => void;
}

/** Analytical inventory grid (TanStack Table). Server-side sorting: header clicks
 *  emit a sort key; the parent re-fetches the page. */
export function InventoryTable({ rows, onSelect, onSortChange }: InventoryTableProps) {
  const [sorting, setSorting] = useState<SortingState>([{ id: 'risk', desc: true }]);

  useEffect(() => {
    const active = sorting[0]?.id;
    if (active) onSortChange(active);
  }, [sorting, onSortChange]);

  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting },
    // The API sorts each column descending only (riskiest / oldest / most-valuable first). Force
    // descending so the ascending state — which never round-trips to the server — can't be shown.
    onSortingChange: (updater) => {
      const next = typeof updater === 'function' ? updater(sorting) : updater;
      const first = next[0];
      setSorting(first ? [{ id: first.id, desc: true }] : sorting);
    },
    manualSorting: true,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className="grid-scroll">
      <table className="data-table">
        <thead>
          {table.getHeaderGroups().map((hg) => (
            <tr key={hg.id}>
              {hg.headers.map((header) => {
                const sortable = header.column.getCanSort();
                const sorted = header.column.getIsSorted();
                return (
                  <th
                    key={header.id}
                    className={`${sortable ? 'sortable' : ''} ${numericColumns.has(header.column.id) ? 'num' : ''}`}
                    onClick={sortable ? header.column.getToggleSortingHandler() : undefined}
                    aria-sort={sorted === 'desc' ? 'descending' : sorted === 'asc' ? 'ascending' : undefined}
                  >
                    {flexRender(header.column.columnDef.header, header.getContext())}
                    {sortable ? <span aria-hidden> {sorted === false ? '↕' : sorted === 'desc' ? '↓' : '↑'}</span> : null}
                  </th>
                );
              })}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr key={row.id} onClick={() => onSelect(row.original.stockId)} tabIndex={0}
              onKeyDown={(e) => { if (e.key === 'Enter') onSelect(row.original.stockId); }}>
              {row.getVisibleCells().map((cell) => (
                <td key={cell.id} className={numericColumns.has(cell.column.id) ? 'num' : ''}>
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
