import { useQuery } from '@tanstack/react-query';
import { apiGet, buildQuery } from './client';

export interface AccuracyMetrics {
  wmape: number | null;
  mae: number;
  rmse: number;
  bias: number | null;
  biasAbs: number;
  mape: number | null;
  overPct: number;
  underPct: number;
  n: number;
}

export interface HistoryPoint {
  month: string;
  label: string;
  value: number;
  isHold: boolean;
}

export interface BacktestPoint {
  month: string;
  label: string;
  actual: number;
  forecast: number;
}

export interface FuturePoint {
  month: string;
  label: string;
  value: number;
  lo: number;
  hi: number;
}

export interface MethodComparison {
  key: string;
  name: string;
  wmape: number | null;
  bias: number | null;
  mae: number;
  rmse: number;
  isBest: boolean;
  isChosen: boolean;
}

export interface ForecastExplanation {
  points: string[];
  recent3: number;
  prior12: number;
  changePct: number;
}

export interface ForecastResult {
  history: HistoryPoint[];
  backtest: BacktestPoint[];
  future: FuturePoint[];
  futureSum: number;
  chosen: string;
  chosenName: string;
  best: string;
  methods: MethodComparison[];
  accuracy: AccuracyMetrics;
  sigma: number;
  holdout: number;
  horizon: number;
  trainN: number;
  totalN: number;
  histUnits: number;
  lastMonth: string;
  explanation: ForecastExplanation;
}

export interface ForecastResponse {
  forecast: ForecastResult;
  meta: { monthsCovered: number; historicalUnits: number; generatedAtUtc: string };
}

export interface DimensionAccuracy {
  value: string;
  wmape: number | null;
  bias: number | null;
  mae: number;
  units: number;
  chosenModel: string;
  tendency: string;
}

export interface AccuracyByResponse {
  dimension: string;
  rows: DimensionAccuracy[];
  meta: { monthsCovered: number; historicalUnits: number; generatedAtUtc: string };
}

export interface ForecastFilterOptions {
  brands: string[];
  models: string[];
  variants: string[];
  types: string[];
  locations: string[];
  colours: string[];
  interiors: string[];
  firstMonth: string;
  lastMonth: string;
}

export interface ForecastQuery {
  model?: string[];
  location?: string[];
  horizon?: number;
  holdout?: number;
  ci?: number;
  algo?: string;
}

const base = '/api/v1/forecasting';

export function useForecast(query: ForecastQuery) {
  return useQuery({
    queryKey: ['forecasting', 'forecast', query],
    queryFn: ({ signal }) => apiGet<ForecastResponse>(`${base}/forecast${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useAccuracyBy(dimension: string, query: Omit<ForecastQuery, 'horizon' | 'ci' | 'algo'>) {
  return useQuery({
    queryKey: ['forecasting', 'accuracy-by', dimension, query],
    queryFn: ({ signal }) =>
      apiGet<AccuracyByResponse>(`${base}/accuracy-by/${dimension}${buildQuery({ ...query })}`, signal),
    staleTime: 30_000,
  });
}

export function useForecastFilterOptions() {
  return useQuery({
    queryKey: ['forecasting', 'filter-options'],
    queryFn: ({ signal }) => apiGet<ForecastFilterOptions>(`${base}/filter-options`, signal),
    staleTime: 300_000,
  });
}
