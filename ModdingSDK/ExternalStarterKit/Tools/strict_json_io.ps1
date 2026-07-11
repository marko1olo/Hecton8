$script:H8StrictUtf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true
$script:H8ReadChunkBytes = 8192
$script:H8GeneratedOrTransientPathNames = @(
    'Generated',
    'Reports',
    '.DS_Store',
    '.tmp',
    'Temp',
    'Library',
    'Logs',
    'obj',
    'bin',
    '.git',
    '.vs',
    'node_modules',
    '__MACOSX',
    '$RECYCLE.BIN',
    'System Volume Information'
)

function Test-H8WindowsPlatform {
    $isWindowsVariable = Get-Variable -Name IsWindows -Scope Global -ErrorAction SilentlyContinue
    if ($null -ne $isWindowsVariable) {
        return [bool]$isWindowsVariable.Value
    }

    return [System.IO.Path]::DirectorySeparatorChar -eq '\'
}

function Add-H8FileLinkNativeType {
    if (-not (Test-H8WindowsPlatform)) {
        return
    }

    if ('H8FileLinkNative' -as [type]) {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class H8FileLinkNative
{
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint FILE_SHARE_DELETE = 0x00000004;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }
}
'@
}

function Get-H8WindowsFileLinkCount([string]$Path) {
    Add-H8FileLinkNativeType

    $shareMode = [H8FileLinkNative]::FILE_SHARE_READ -bor [H8FileLinkNative]::FILE_SHARE_WRITE -bor [H8FileLinkNative]::FILE_SHARE_DELETE
    $handle = [H8FileLinkNative]::CreateFile(
        $Path,
        0,
        $shareMode,
        [IntPtr]::Zero,
        [H8FileLinkNative]::OPEN_EXISTING,
        [H8FileLinkNative]::FILE_FLAG_BACKUP_SEMANTICS,
        [IntPtr]::Zero)

    if ($null -eq $handle -or $handle.IsInvalid) {
        $errorCode = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw ('Unable to inspect NTFS link count for ' + $Path + ' (Win32 ' + $errorCode + ').')
    }

    try {
        $info = New-Object 'H8FileLinkNative+BY_HANDLE_FILE_INFORMATION'
        if (-not [H8FileLinkNative]::GetFileInformationByHandle($handle, [ref]$info)) {
            $errorCode = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
            throw ('Unable to inspect NTFS link count for ' + $Path + ' (Win32 ' + $errorCode + ').')
        }

        return [uint32]$info.nNumberOfLinks
    } finally {
        $handle.Dispose()
    }
}

function Get-H8UnixFileLinkCount([string]$Path) {
    $stat = Get-Command stat -ErrorAction SilentlyContinue
    if ($null -eq $stat) {
        throw ('Unable to inspect filesystem link count for ' + $Path + ': stat command is unavailable.')
    }

    $output = @(& $stat.Source -c '%h' -- $Path 2>$null)
    if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0 -or [string]::IsNullOrWhiteSpace([string]$output[0])) {
        $output = @(& $stat.Source -f '%l' $Path 2>$null)
    }

    [int]$linkCount = 0
    if ($output.Count -eq 0 -or -not [int]::TryParse(([string]$output[0]).Trim(), [ref]$linkCount)) {
        throw ('Unable to inspect filesystem link count for ' + $Path + '.')
    }

    return $linkCount
}

function Get-H8FileLinkCount([System.IO.FileInfo]$Item) {
    if (Test-H8WindowsPlatform) {
        return Get-H8WindowsFileLinkCount $Item.FullName
    }

    return Get-H8UnixFileLinkCount $Item.FullName
}

function Assert-NoFilesystemLinks([object]$Item) {
    if ($null -eq $Item) {
        throw '[H8MOD_FILESYSTEM] Filesystem item is null.'
    }

    $resolvedItem = $Item
    if ($Item -is [string]) {
        $resolvedItem = Get-Item -LiteralPath ([string]$Item) -Force -ErrorAction Stop
    }

    $fullName = [string]$resolvedItem.FullName
    if ([string]::IsNullOrWhiteSpace($fullName)) {
        throw '[H8MOD_FILESYSTEM] Filesystem item has no full path.'
    }

    if (($resolvedItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw ('[H8MOD_FILESYSTEM] Filesystem links are banned: ' + $fullName)
    }

    foreach ($propertyName in @('LinkType','Target','LinkTarget')) {
        $property = $resolvedItem.PSObject.Properties[$propertyName]
        if ($null -ne $property -and $null -ne $property.Value) {
            $valueText = [string]$property.Value
            if (-not [string]::IsNullOrWhiteSpace($valueText)) {
                throw ('[H8MOD_FILESYSTEM] Filesystem links are banned: ' + $fullName + ' (' + $propertyName + '=' + $valueText + ')')
            }
        }
    }

    if ($resolvedItem -is [System.IO.FileInfo]) {
        $linkCount = Get-H8FileLinkCount $resolvedItem
        if ($linkCount -gt 1) {
            throw ('[H8MOD_FILESYSTEM] Hardlinks are banned: ' + $fullName + ' has ' + $linkCount + ' links.')
        }
    }
}

function Assert-NoPathFilesystemLinks([string]$BasePath, [string]$RelativePath, [bool]$RequireLeaf) {
    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        throw '[H8MOD_FILESYSTEM] Base path is required for link validation.'
    }
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        throw '[H8MOD_FILESYSTEM] Relative path is required for link validation.'
    }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        throw ('[H8MOD_FILESYSTEM] Link validation path must be relative: ' + $RelativePath)
    }

    $current = (Resolve-Path -LiteralPath $BasePath).Path
    Assert-NoFilesystemLinks (Get-Item -LiteralPath $current -Force)

    foreach ($segment in ($RelativePath.Replace('\','/') -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
            throw ('[H8MOD_FILESYSTEM] Invalid path segment during link validation: ' + $RelativePath)
        }

        $current = Join-Path $current $segment
        if (-not (Test-Path -LiteralPath $current)) {
            throw ('[H8MOD_FILESYSTEM] Missing path during link validation: ' + $RelativePath)
        }

        Assert-NoFilesystemLinks (Get-Item -LiteralPath $current -Force)
    }

    if ($RequireLeaf -and -not (Test-Path -LiteralPath $current -PathType Leaf)) {
        throw ('[H8MOD_FILESYSTEM] Expected file during link validation: ' + $RelativePath)
    }
    if ((-not $RequireLeaf) -and -not (Test-Path -LiteralPath $current -PathType Container)) {
        throw ('[H8MOD_FILESYSTEM] Expected directory during link validation: ' + $RelativePath)
    }
}

function Assert-H8PathExactCase([string]$BasePath, [string]$RelativePath, [bool]$RequireLeaf) {
    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        throw '[H8MOD_FILESYSTEM] Base path is required for exact-case validation.'
    }
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        throw '[H8MOD_FILESYSTEM] Relative path is required for exact-case validation.'
    }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        throw ('[H8MOD_FILESYSTEM] Exact-case validation path must be relative: ' + $RelativePath)
    }

    $current = (Resolve-Path -LiteralPath $BasePath).Path
    Assert-NoFilesystemLinks (Get-Item -LiteralPath $current -Force)

    foreach ($segment in ($RelativePath.Replace('\','/') -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
            throw ('[H8MOD_FILESYSTEM] Invalid path segment during exact-case validation: ' + $RelativePath)
        }
        if (-not (Test-Path -LiteralPath $current -PathType Container)) {
            throw ('[H8MOD_FILESYSTEM] Missing parent directory during exact-case validation: ' + $RelativePath)
        }

        $exactChild = $null
        foreach ($child in (Get-ChildItem -LiteralPath $current -Force)) {
            Assert-NoFilesystemLinks $child
            if ([string]::Equals($child.Name, $segment, [System.StringComparison]::Ordinal)) {
                $exactChild = $child
                break
            }
        }

        if ($null -eq $exactChild) {
            throw ('[H8MOD_FILESYSTEM] Exact-case path mismatch or missing path: ' + $RelativePath)
        }

        Assert-NoFilesystemLinks $exactChild
        $current = $exactChild.FullName
    }

    if ($RequireLeaf -and -not (Test-Path -LiteralPath $current -PathType Leaf)) {
        throw ('[H8MOD_FILESYSTEM] Expected file during exact-case validation: ' + $RelativePath)
    }
    if ((-not $RequireLeaf) -and -not (Test-Path -LiteralPath $current -PathType Container)) {
        throw ('[H8MOD_FILESYSTEM] Expected directory during exact-case validation: ' + $RelativePath)
    }
}

function Assert-NoCaseFoldDuplicates([object[]]$RelativePaths) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $firstByCaseFold = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($rawPath in @($RelativePaths)) {
        $path = [string]$rawPath
        if ([string]::IsNullOrWhiteSpace($path)) {
            throw '[H8MOD_FILESYSTEM] Empty relative path found during case-fold duplicate scan.'
        }

        $normalized = $path.Replace('\','/')
        if (-not $seen.Add($normalized)) {
            $first = $firstByCaseFold[$normalized]
            if ([string]::Equals($first, $normalized, [System.StringComparison]::Ordinal)) {
                throw ('[H8MOD_FILESYSTEM] Duplicate relative path found: ' + $normalized)
            }

            throw ('[H8MOD_FILESYSTEM] Case-fold duplicate relative paths found: ' + $first + ' vs ' + $normalized)
        }

        $firstByCaseFold[$normalized] = $normalized
    }
}

function Test-H8GeneratedOrTransientPath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $false
    }

    $normalized = $RelativePath.Replace('\','/')
    foreach ($segment in ($normalized -split '/')) {
        foreach ($blockedName in $script:H8GeneratedOrTransientPathNames) {
            if ([string]::Equals($segment, $blockedName, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }

    $leaf = [System.IO.Path]::GetFileName($normalized)
    if ([string]::Equals($leaf, '.DS_Store', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    if ($leaf.EndsWith('.tmp', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $false
}

function Get-H8SafeSourceFiles([string]$RootPath, [switch]$ExcludeGeneratedOrTransient) {
    if ([string]::IsNullOrWhiteSpace($RootPath)) {
        throw '[H8MOD_FILESYSTEM] Root path is required for source traversal.'
    }

    $rootFull = (Resolve-Path -LiteralPath $RootPath).Path
    $rootItem = Get-Item -LiteralPath $rootFull -Force
    if ($rootItem -isnot [System.IO.DirectoryInfo]) {
        throw ('[H8MOD_FILESYSTEM] Source traversal root must be a directory: ' + $rootFull)
    }

    Assert-NoFilesystemLinks $rootItem

    $rootPrefix = $rootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $directories = New-Object 'System.Collections.Generic.Stack[System.IO.DirectoryInfo]'
    $files = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
    $directories.Push($rootItem)

    while ($directories.Count -gt 0) {
        $directory = $directories.Pop()
        Assert-NoFilesystemLinks $directory

        foreach ($child in (Get-ChildItem -LiteralPath $directory.FullName -Force)) {
            Assert-NoFilesystemLinks $child

            $fullPath = [System.IO.Path]::GetFullPath($child.FullName)
            if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw ('[H8MOD_FILESYSTEM] Traversal escaped source root: ' + $fullPath)
            }

            $relative = $fullPath.Substring($rootPrefix.Length).Replace('\','/')
            if ($ExcludeGeneratedOrTransient -and (Test-H8GeneratedOrTransientPath $relative)) {
                continue
            }

            if ($child -is [System.IO.DirectoryInfo]) {
                $directories.Push($child)
            } elseif ($child -is [System.IO.FileInfo]) {
                [void]$files.Add($child)
            }
        }
    }

    return $files.ToArray()
}

function Read-H8TextFileCapped([string]$Path, [string]$Label, [long]$MaxBytes) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw ($Label + ' is missing: ' + $Path)
    }
    if ($MaxBytes -le 0) {
        throw ($Label + ' byte cap must be positive.')
    }
    if ($MaxBytes -ge [int]::MaxValue) {
        throw ($Label + ' byte cap is too large.')
    }

    $fileItem = Get-Item -LiteralPath $Path -Force
    Assert-NoFilesystemLinks $fileItem

    $buffer = New-Object byte[] ([int]($MaxBytes + 1))
    $totalBytes = 0
    $exceeded = $false

    try {
        $stream = [System.IO.File]::Open($fileItem.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            while ($true) {
                $readSize = [System.Math]::Min($script:H8ReadChunkBytes, [int]($buffer.Length - $totalBytes))
                if ($readSize -le 0) {
                    $exceeded = $true
                    break
                }

                $read = $stream.Read($buffer, $totalBytes, $readSize)
                if ($read -le 0) {
                    break
                }

                $totalBytes += $read
                if ($totalBytes -gt $MaxBytes) {
                    $exceeded = $true
                    break
                }
            }
        } finally {
            $stream.Dispose()
        }
    } catch {
        throw ($Label + ' read failed: ' + $_.Exception.Message)
    }

    if ($exceeded) {
        throw ($Label + ' exceeds byte cap: ' + $MaxBytes)
    }

    try {
        return $script:H8StrictUtf8NoBom.GetString($buffer, 0, [int]$totalBytes)
    } catch [System.Text.DecoderFallbackException] {
        throw ($Label + ' is not strict UTF-8.')
    }
}

function Read-H8JsonFileCapped([string]$Path, [string]$Label, [long]$MaxBytes) {
    $jsonText = Read-H8TextFileCapped $Path $Label $MaxBytes
    try {
        return $jsonText | ConvertFrom-Json
    } catch {
        throw ($Label + ' is invalid JSON: ' + $_.Exception.Message)
    }
}
