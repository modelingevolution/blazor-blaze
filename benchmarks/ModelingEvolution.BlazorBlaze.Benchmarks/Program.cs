using BenchmarkDotNet.Running;
using ModelingEvolution.BlazorBlaze.Benchmarks;

// Run all benchmarks
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
