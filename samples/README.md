# OKF sample bundles

Run the checker against the failure showcase bundle:

```bash
okf check samples/check-failures
```

This bundle is **intentionally invalid**. Each file triggers a specific diagnostic.

| File | Expected error |
|------|----------------|
| `concepts/missing-frontmatter.md` | Missing YAML frontmatter block |
| `concepts/unterminated-frontmatter.md` | Unterminated YAML frontmatter block |
| `concepts/invalid-yaml.md` | Invalid YAML in frontmatter |
| `concepts/missing-type.md` | Missing non-empty `type` field |
| `concepts/empty-type.md` | Missing non-empty `type` field |
| `concepts/broken-relative-link.md` | Broken relative link |
| `concepts/broken-absolute-link.md` | Broken bundle-rooted (`/…`) link |
| `index.md` | Unexpected root index frontmatter keys; missing section; missing list entry; broken link |
| `bad-index/frontmatter/index.md` | index.md must not contain frontmatter |
| `bad-index/malformed-entry/index.md` | Index entry format |
| `bad-index/broken-link/index.md` | Broken link in index |
| `log.md` | Frontmatter forbidden; invalid date; non-ISO heading; non-list entry; broken link |
| `empty-log/log.md` | Missing date heading |

External URLs (for example `https://example.com`) are ignored and do not fail the check.
