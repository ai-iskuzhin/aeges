# Project Organization

Aeges separates discovery roots from project groups.

A discovery root is a filesystem boundary that Aeges may scan for projects. It
does not classify projects by itself. A project group is an explicit
classification, optionally backed by a filesystem path.

For a layout such as:

```text
~/work/
├── aeges/
├── notes/
├── analitex/
│   ├── api/
│   └── web/
└── brsc/
    └── portal/
```

scan `~/work` first. Projects directly under `~/work` remain ungrouped.
Projects inside likely group folders such as `~/work/analitex` and
`~/work/brsc` are assigned to inferred groups during scan.

Example:

```bash
aeges root scan "$HOME/work"
aeges root scan "$HOME/work" --apply
```

The dry run reports the root, inferred groups, and project candidates. `--apply`
persists newly discovered roots, groups, and projects. Registered root ids still
work for scripted use:

```bash
aeges root add --root-id work --name Work --path "$HOME/work"
aeges group add --group-id analitex --name Analitex --path "$HOME/work/analitex"
aeges root scan work --apply
```

`root scan` looks two levels deep by default. It treats common project markers as project boundaries, including
`.git`, `.sln`, `.csproj`, `package.json`, `pyproject.toml`, `Cargo.toml`,
`go.mod`, `deno.json`, and `deno.jsonc`.

The scanner ignores heavy or generated folders such as `.git`, `.aeges`,
`node_modules`, `bin`, `obj`, `dist`, `build`, and `vendor`.
