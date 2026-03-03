# Copilot Instructions

These instructions apply to every code change in this workspace.

## Reference Documents

- `spec.md` is the **source of truth** for all requirements. Consult it before making changes. Keep it updated when requirements change.
- `prompt.md` contains the original implementation plan (completed). It may be useful for architectural context.

## After Every Code Change

After completing each discrete change (bug fix, feature, refactoring, etc.):

1. **Adversarial code review** — Before committing, review the code created or changed:
   - Critique for problems with **single responsibility, SOLID principles, naming, error handling, correctness, and readability**.
   - List all findings.
   - Implement **only** the fixes that make a **relevant difference to code correctness and readability**. Skip nitpicks, stylistic preferences, and over-engineering suggestions that add complexity without clear benefit.

2. **Build verification** — Run `dotnet build` on the affected project(s) to confirm no compilation errors.

3. **Git commit** — Stage and commit the changes with a concise message describing what was done.

4. **Update spec.md** — If the change affects behavior, UX, or architecture, update `spec.md` accordingly (including the Revision History table).
