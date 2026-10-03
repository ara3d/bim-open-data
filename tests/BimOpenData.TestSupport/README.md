# BimOpenData.TestSupport

Shared helpers for the test projects in `tests/`: `RepoPaths` (the checkout
root and its `samples/`, `data/`, and `artifacts/` folders, located from the
source file so out-of-tree build output still works) and `MiniIfc` (a
fourteen-line IFC4 wall file and a `Document(...)` builder for other
hand-written STEP fixtures).

It references no `src` project and no test framework. It started as a copy of
bim-open-toolkit's `tests/BimOpenToolkit.TestSupport`, which the toolkit keeps
for its own tests; `RepoPaths` differs only in the solution file it looks for.
