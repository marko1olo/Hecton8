cat << 'CSPROJ' > test_build.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Assets/_Project/Scripts/HectonCelestialEngine.cs" />
  </ItemGroup>
</Project>
CSPROJ
dotnet build test_build.csproj
