# Code quality and maintainability

These rules are normative for this repository. They apply to new code and to
code touched during a change. Detailed format knowledge belongs in `docs/`; code
comments stay short.

## Ownership and lifetime

- Parsers and readers return immutable results. Do not expose mutable internal
  arrays or lists; use `IReadOnlyList<T>` / `ReadOnlyMemory<byte>` at boundaries.
- Accept `ReadOnlySpan<byte>` or `ReadOnlyMemory<byte>` for input. Do not retain
  the input buffer unless the API clearly documents that it does.
- File, archive, native, and UI objects are owned by the code that creates them.
  Use `using` for `IDisposable` objects and make the disposal order obvious.
- Native handles use `SafeHandle` (or an equivalent wrapper). Do not rely on
  finalizers for correctness.
- Constructors do no heavy I/O. Use `Open`/`Load`/`Parse` factories for work that
  can fail.

## Errors, fallbacks, and context

- Validate public inputs early and fail fast with a specific exception type.
- Error messages must identify the failing stage and useful context, for example:
  `TRINS parse failed (entry=12, offset=0x1C, count=6): payload is truncated.`
- Include file path, entry index, offset, count, or format name when known.
- Do not swallow exceptions. Catch only what can be handled; otherwise let the
  exception propagate.
- Do not use `catch (Exception)` except at a process or UI boundary, where it must
  be logged and surfaced to the user.
- Fallbacks must be explicit: distinguish "optional/absent" from
  "corrupt/unsupported". Never silently substitute a default for corrupt data.
- If a fallback is chosen, record why and what was assumed.

## Constants and configuration

- Name magic numbers, limits, strides, alignment values, and thresholds.
  Group shared values in a small static class when they are reused.
- Validate configuration values at startup; do not spread unchecked literals
  through parsers and UI code.
- Allocation limits and input-size caps are part of the format contract and need
  named constants.

## Workarounds

- Mark workarounds with a short `// WORKAROUND:` comment stating the reason, the
  condition under which it applies, and where the issue is documented.
- Keep the workaround as narrow as possible. Do not let it become the default
  path for valid data.

## Control flow

- Prefer guard clauses and early returns. Keep nesting shallow and control flow
  linear.
- Separate detection, validation, and parsing steps.
- Do not refactor unrelated code or reformat whole files during a focused change.
- Avoid speculative abstractions; add them when a second caller needs them.

## Comments, identifiers, and documentation

- Identifiers, comments, and project documentation are written in English.
- Code comments state intent, preconditions, or limitations. Keep them to at most
  four lines. Do not comment on what the code already says through names or types.
- Delete stale or obvious comments when editing the surrounding code.
- Put format layouts, schemas, evidence, and open questions in `docs/`.
- Use `// WORKAROUND:`, `// TODO:`, and `// HYPOTHESIS:` deliberately; each must
  explain why it exists.

## Change hygiene

- Keep diffs focused. No drive-by renames, broad formatting changes, or
  whitespace churn.
- Follow `.editorconfig`: UTF-8, LF text files, existing naming conventions.
- New formats are documented in `docs/` before or with their Core implementation.
- Add tests for bounds checks, malformed input, and real dump data where
  appropriate. Tests must be deterministic and must not commit ROM content.

## Review checklist

- Is ownership clear, and is every disposable released?
- Does every failure say what failed and where?
- Are limits and thresholds named and validated?
- Is any fallback or workaround explicit and documented?
- Is nesting shallow and the diff focused?
- Are comments short, English, and non-obvious?
