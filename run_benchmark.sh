#!/bin/bash
/opt/unity/Editor/Unity -quit -batchmode -projectPath . -executeMethod BenchmarkRunner.RunBenchmark -logFile benchmark_log.txt
cat benchmark_log.txt | grep -E "CalculateSheetBounds"
