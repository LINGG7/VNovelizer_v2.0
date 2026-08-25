# CDN release workflow

## Build gate

1. Set `PlayerSettings.bundleVersion` to the content version.
2. Run **VNovelizer > Addressables > Sync CDN Version**.
3. Build Addressables content, then build the player. The pre-build validator rejects:
   - a mismatched player, Addressables, or TapTap CDN version;
   - a disabled remote catalog or an infinite catalog timeout;
   - a `*-Remote` group using local build/load paths;
   - a remote group without a provider timeout.

Do not reuse a published version directory. Bundle names are content-hashed and the
version directory is immutable.

## Atomic publish order

1. Upload all `.bundle` files to a temporary release prefix.
2. Verify file sizes and hashes, then request each bundle from the public CDN edge.
3. Promote/copy bundles to `/vnovelizer/prod/<version>/<platform>/`.
4. Upload the catalog JSON only after every referenced bundle is available.
5. Upload the catalog hash last. This is the release commit point.
6. Start a clean-cache player and finish the required-content preload before marking
   the release healthy.

Recommended response headers:

- Hashed bundles: `Cache-Control: public, max-age=31536000, immutable`
- Catalog and hash: short cache lifetime or explicit revalidation
- WebGL/TapTap: correct `Access-Control-Allow-Origin`, `Content-Length`, and byte-range
  support where the host platform requires them

Rollback by restoring the preceding catalog/hash pair. Do not delete old version
directories while supported clients may still reference them.

## Runtime failover

The client stays on one content version when changing hosts:

```csharp
RemoteContentPreloadManager.GetInstance().ConfigureCdnFailover(
    "https://primary.example.com/vnovelizer/prod/1.1.3/",
    "https://backup.example.com/vnovelizer/prod/1.1.3/");
```

The call rejects roots whose `/prod/<version>/` segments differ. A backup host is
used only after a server-unavailable or missing-resource response.

## Monitoring

Alert on catalog/bundle 404s, 5xx rate, edge cache-hit rate, time to first byte, and
required-content completion rate. Client telemetry uses the
`[RemoteContentTelemetry]` prefix and intentionally strips URL query strings.
