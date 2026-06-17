Add-Type -AssemblyName System.Drawing

$assets = "C:\Users\gungaretti\source\repos\personal\PowerModeSlider\PowerModeSlider\Assets"
$sizes  = @(16,20,24,32,40,48,64,128,256)

# Map: base 64x64 png -> output awake ico
$map = @{
    "PowerEfficiency.png"  = "PowerEfficiencyAwake.ico"
    "PowerBalanced.png"    = "PowerBalancedAwake.ico"
    "PowerPerformance.png" = "PowerPerformanceAwake.ico"
}

function New-AwakeFrame {
    param([System.Drawing.Image]$Base, [int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Draw the base power-mode icon, slightly shrunk so the badge has breathing room.
    $inset = [Math]::Max(1, [int]($Size * 0.04))
    $drawSize = $Size - 2 * $inset
    $g.DrawImage($Base, $inset, $inset, $drawSize, $drawSize)

    # Awake badge: amber "caffeine" dot with a white ring, bottom-right corner.
    $d = [double]$Size * 0.48        # badge diameter
    if ($d -lt 7) { $d = 7 }
    $bx = $Size - $d - ($Size * 0.02)
    $by = $Size - $d - ($Size * 0.02)
    $ring = [Math]::Max(1.0, $Size * 0.06)

    # White outline ring for contrast against any tray background.
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,255,255,255))
    $g.FillEllipse($white, [float]($bx - $ring), [float]($by - $ring), [float]($d + 2*$ring), [float]($d + 2*$ring))

    # Amber fill (keep-awake / caffeine accent).
    $amber = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,255,159,10))
    $g.FillEllipse($amber, [float]$bx, [float]$by, [float]$d, [float]$d)

    # Tiny dark "core" glyph so the badge reads as an active indicator even at 16px.
    if ($Size -ge 24) {
        $core = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,40,20,0))
        $cd = $d * 0.34
        $cx = $bx + ($d - $cd) / 2
        $cy = $by + ($d - $cd) / 2
        $g.FillEllipse($core, [float]$cx, [float]$cy, [float]$cd, [float]$cd)
        $core.Dispose()
    }

    $white.Dispose(); $amber.Dispose(); $g.Dispose()
    return $bmp
}

function Save-IcoFromPngFrames {
    param([byte[][]]$Pngs, [int[]]$FrameSizes, [string]$Path)

    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $count = $Pngs.Length

    # ICONDIR
    $bw.Write([UInt16]0)   # reserved
    $bw.Write([UInt16]1)   # type = icon
    $bw.Write([UInt16]$count)

    $offset = 6 + 16 * $count
    for ($i = 0; $i -lt $count; $i++) {
        $w = $FrameSizes[$i]; $h = $FrameSizes[$i]
        $bw.Write([Byte]($(if ($w -ge 256) {0} else {$w})))
        $bw.Write([Byte]($(if ($h -ge 256) {0} else {$h})))
        $bw.Write([Byte]0)     # color count
        $bw.Write([Byte]0)     # reserved
        $bw.Write([UInt16]1)   # planes
        $bw.Write([UInt16]32)  # bit count
        $bw.Write([UInt32]$Pngs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $Pngs[$i].Length
    }
    foreach ($p in $Pngs) { $bw.Write($p) }
    $bw.Flush(); $bw.Close(); $fs.Close()
}

foreach ($src in $map.Keys) {
    $basePath = Join-Path $assets $src
    $base = [System.Drawing.Image]::FromFile($basePath)

    $pngs = @()
    foreach ($s in $sizes) {
        $frame = New-AwakeFrame -Base $base -Size $s
        $ms = New-Object System.IO.MemoryStream
        $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += ,($ms.ToArray())
        $ms.Dispose(); $frame.Dispose()
    }
    $base.Dispose()

    $outPath = Join-Path $assets $map[$src]
    Save-IcoFromPngFrames -Pngs $pngs -FrameSizes $sizes -Path $outPath
    Write-Host "Wrote $outPath ($($pngs.Length) frames)"
}
