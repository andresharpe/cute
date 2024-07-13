function Get-Screen
{
    param(
        [string]$FileName, [int]$CropTop = 75, [int]$CropBottom = 100
    )


    begin {
        Add-Type -AssemblyName System.Drawing
    }
    process {

        $Width  = 2880
        $Height = 1920 - $CropTop - $CropBottom
        $Left   = 0
        $Top    = 0
        
        $bitmap  = New-Object System.Drawing.Bitmap $Width, $Height
        $graphic = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphic.CopyFromScreen($Left, $Top+$CropTop, 0, 0, $bitmap.Size)
        
        $AbsoluteFileName = [IO.Path]::Combine((Get-Location).Path,$FileName)

        $bitmap.Save($AbsoluteFileName)
    }
}

