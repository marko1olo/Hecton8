mkdir temp_bench && cd temp_bench
dotnet new console
cp ../benchmark_csharp.cs Program.cs
dotnet run
cd ..
rm -rf temp_bench
