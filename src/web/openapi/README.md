# OpenAPI contract snapshot & type generation

`openapi.json` in this directory is the **committed contract snapshot**. The CI
`openapi` job exports a fresh document from the built API and fails on any diff
against this file — so an API surface change that isn't deliberately re-exported
and committed here breaks the build instead of silently drifting away from the SPA.

## Regenerating after an API change

```bash
# 1. Run the API host (no database needed for the document)
dotnet run --project ../../api/BeeEye.Api      # serves http://localhost:5080

# 2. Re-export the contract snapshot (commit the result with your API change)
curl -s http://localhost:5080/openapi/v1.json > openapi.json

# 3. Regenerate TypeScript types (git-ignored)
npm run gen:api                                 # -> src/lib/api/schema.d.ts
```

Until call sites migrate to the generated `schema.d.ts`, the hand-authored types
in `src/lib/api/types.ts` + `src/lib/api/*.ts` are the SPA's working types — the
committed snapshot diff is what keeps them honest in the meantime.
