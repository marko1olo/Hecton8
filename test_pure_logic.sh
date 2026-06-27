PROJECT_ROOT=$PWD
mkdir -p /tmp/hecton_tests
cd /tmp/hecton_tests
dotnet new nunit -n PureLogicTests -f net8.0 --force
cd PureLogicTests
rm UnitTest1.cs || true
mkdir -p Systems Tests
cp $PROJECT_ROOT/Assets/_Project/Scripts/PureLogic/Systems/CeilingConcavityAirPocketVolumeCalculator.cs Systems/
cp $PROJECT_ROOT/Assets/_Project/Scripts/PureLogic/Tests/CeilingConcavityAirPocketVolumeCalculatorTests.cs Tests/
cat << 'CSPROJ' > PureLogicTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <UseAppHost>false</UseAppHost>
    <SelfContained>false</SelfContained>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="4.0.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
</Project>
CSPROJ
dotnet test
