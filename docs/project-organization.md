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

register `~/work` as a root, then register `~/work/analitex` and `~/work/brsc`
as groups. Projects directly under `~/work` remain ungrouped. Projects inside a
group path are assigned to the most specific matching group during scan.

Example:

```bash
aeges root add --root-id work --name Work --path "$HOME/work"

aeges group add \
  --group-id analitex \
  --name Analitex \
  --path "$HOME/work/analitex"

aeges group add \
  --group-id brsc \
  --name BRSC \
  --path "$HOME/work/brsc"

aeges root scan work --max-depth 2
aeges root scan work --max-depth 2 --apply
```

`root scan` treats common project markers as project boundaries, including
`.git`, `.sln`, `.csproj`, `package.json`, `pyproject.toml`, `Cargo.toml`,
`go.mod`, `deno.json`, and `deno.jsonc`.

The scanner ignores heavy or generated folders such as `.git`, `.aeges`,
`node_modules`, `bin`, `obj`, `dist`, `build`, and `vendor`.
