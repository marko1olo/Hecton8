$script:H8StrictUtf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true
$script:H8ReadChunkBytes = 8192

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

    $buffer = New-Object byte[] ([int]($MaxBytes + 1))
    $totalBytes = 0
    $exceeded = $false

    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
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
