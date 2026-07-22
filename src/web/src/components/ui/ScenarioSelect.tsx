interface Option {
  value: string;
  label: string;
}

interface ScenarioSelectProps {
  label: string;
  options: readonly Option[];
  value: string;
  onChange: (value: string) => void;
}

/** Labelled select for scenario parameters (no "All" option — always a concrete value). */
export function ScenarioSelect({ label, options, value, onChange }: ScenarioSelectProps) {
  return (
    <label className="filter-select">
      <span>{label}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}
