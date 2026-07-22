# API type generation

The web app's typed API client is generated from the platform's OpenAPI document.

```bash
# 1. Run the API host
dotnet run --project ../../api/BeeEye.Api      # serves http://localhost:5080

# 2. Snapshot the OpenAPI document
curl -s http://localhost:5080/openapi/v1.json > openapi.json

# 3. Generate TypeScript types
npm run gen:api                                 # -> src/lib/api/schema.d.ts
```

Until `schema.d.ts` is generated, the hand-authored types in `src/lib/api/types.ts`
are used. Migrate call sites to the generated types once available.
