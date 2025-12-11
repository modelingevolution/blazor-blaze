using BenchmarkDotNet.Running;
using BlazorBlaze.Benchmarks;

// Run all benchmarks
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
