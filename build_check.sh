#!/bin/bash
cat << 'CSPROJ' > Dummy.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Assets/_Project/Scripts/SaveBinaryStorage.cs" />
  </ItemGroup>
</Project>
CSPROJ
dotnet build Dummy.csproj
