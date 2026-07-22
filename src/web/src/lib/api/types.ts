/**
 * API contract types. For the scaffold these are hand-authored; run
 * `npm run gen:api` to regenerate `schema.d.ts` from the live OpenAPI document
 * (`/openapi/v1.json`) and migrate these to the generated types.
 */

export interface ModuleInfo {
  name: string;
  routePrefix: string;
  description: string;
  status: string;
}

/** RFC 7807 Problem Details, as emitted by the API. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  correlationId?: string;
}
