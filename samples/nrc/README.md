# NRC Duplex fixtures

Three files copied on 2026-10-03 from bim-open-toolkit's `samples/nrc/`, where
the NRC paper's graphs and walkthrough use them. Here they are test fixtures:

| File | Read by |
|---|---|
| `duplex-base.ifc` | `BimOpenMcp.Ifc.Tests` (a model with no metric provenance) |
| `duplex-enriched.ifc` | `BimOpenMcp.Ifc.Tests` (storey totals, the metric catalog) and `Ara3D.BimOpenSchema.DuckDb.Tests` (`IfcDuckDbBuildTests`) |
| `nrc-metrics.csv` | The metric dictionary that `duplex-enriched.ifc` names in `Pset_NRCAnalyticsProvenance.MetricDictionaryURI`; the IFC MCP server looks for it beside the model |

The toolkit's copies are the source. If they change there, copy them again;
the toolkit's split plan (`docs/plans/repository-split.md`) lists this duplicate
as debt.
