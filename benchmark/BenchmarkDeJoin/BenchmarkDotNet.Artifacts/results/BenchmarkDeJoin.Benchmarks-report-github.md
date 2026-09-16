```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.7517/23H2/2023Update/SunValley3)
13th Gen Intel Core i7-13850HX 2.10GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-CNUJVU : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

InvocationCount=1  UnrollFactor=1  

```
| Method         | NewMethod | Mean    | Error    | StdDev   | Gen0      | Allocated |
|--------------- |---------- |--------:|---------:|---------:|----------:|----------:|
| **LoadDefinition** | **false**     | **8.633 s** | **0.1091 s** | **0.1021 s** | **1000.0000** |  **19.49 MB** |
| **LoadDefinition** | **true**      | **8.593 s** | **0.1689 s** | **0.3333 s** | **1000.0000** |  **19.45 MB** |
