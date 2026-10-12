# Mesher timing (warm)

Written by `Ara3D.Ifc.Mesher.Benchmarks` (BenchmarkDotNet, short job: 3 warm-ups, 8 timed runs each). Times are in milliseconds; the median is the figure to quote.

- Machine: Intel64 Family 6 Model 170 Stepping 4, GenuineIntel, 22 logical cores, Windows 11 (10.0.26200.9457), .NET 8.0.30 (8.0.3026.36720)
- Commit: 4940d85

| file | mesher only (ms) | parse + mesh (ms) | web-ifc DLL (ms) | mesher / web-ifc |
|---|---:|---:|---:|---:|
| duplex.ifc | 109.5 | 118.5 | 102.4 | 1.1 |
| example.ifc | 129.7 | 61.2 | 21.9 | 5.9 |
| IfcOpenHouse_IFC4.ifc | 12.0 | 12.4 | 10.6 | 1.1 |
| steelplates.ifc | 4.6 | 5.2 | 34.0 | 0.1 |
