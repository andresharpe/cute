param(
    [Parameter(Mandatory = $true)]
    [string]$XmlFilePath
)

# Load the XML file
[xml]$xmlDoc = Get-Content $XmlFilePath

# Function to clean Spectre Console markup from descriptions
function Clean-Description {
    param(
        [string]$Text
    )
    if ($null -eq $Text) {
        return ""
    }

    # Replace [italic]...[/] with *...*
    $Text = $Text -replace '\[\/?italic(?: [^\]]+)?\]', '*'

    # Remove color specifications like [LightSkyBlue3]
    $Text = $Text -replace '\[\/?[^\]]+\]', ''

    return $Text.Trim()
}

# Function to extract and clean description text
function Get-DescriptionText {
    param(
        $DescriptionNode
    )
    if ($null -eq $DescriptionNode) {
        return ""
    }
    elseif ($DescriptionNode -is [System.Xml.XmlNode]) {
        # Get all text nodes within the Description element
        $textNodes = $DescriptionNode.SelectNodes('.//text()')
        if ($null -ne $textNodes) {
            $text = $textNodes | ForEach-Object { $_.Value } | Where-Object { $null -ne $_ } | Out-String
            $cleanText = Clean-Description $text
            return $cleanText.Trim()
        }
        else {
            $text = $DescriptionNode.InnerText
            $cleanText = Clean-Description $text
            return $cleanText.Trim()
        }
    }
    else {
        # It's probably a string
        $text = [string]$DescriptionNode.Trim()
        $cleanText = Clean-Description $text
        return $cleanText
    }
}

# Function to process commands recursively
function Process-Command {
    param(
        [System.Xml.XmlNode]$CommandNode,
        [string]$CommandPath = "",
        [int]$Level = 0
    )

    # Get current command name
    $currentCommandName = $CommandNode.Name

    # Build full command path
    if ("" -ne $CommandPath) {
        $fullCommandPath = "$CommandPath $currentCommandName"
    } else {
        $fullCommandPath = "$currentCommandName"
    }

    # Prepend 'cute ' to the command path
    $cuteCommand = "cute $fullCommandPath"

    # Set command heading level to '##'
    $headingLevel = '##'

    # Get command description
    $commandDescription = Get-DescriptionText $CommandNode.Description

    # Output command name and description
    Write-Output "$headingLevel $cuteCommand`n"
    if ($null -ne $commandDescription -and "" -ne $commandDescription) {
        Write-Output "$commandDescription`n"
    }

    # Process parameters
    if ($null -ne $CommandNode.Parameters.Option) {
        Write-Output "### Parameters`n"
        # Output table header
        Write-Output "| Option | Required | Description |"
        Write-Output "|--------|----------|-------------|"

        foreach ($param in $CommandNode.Parameters.Option) {
            $shortName = $param.Short
            $longName = $param.Long
            $value = $param.Value
            $required = $param.Required

            # Get parameter description
            $paramDescription = Get-DescriptionText $param.Description

            $optionSyntax = ""
            if ($null -ne $shortName -and "" -ne $shortName) {
                $optionSyntax += "-$shortName"
            }
            if ($null -ne $longName -and "" -ne $longName) {
                if ($optionSyntax) {
                    $optionSyntax += ", "
                }
                $optionSyntax += "--$longName"
            }
            if ($null -ne $value -and "NULL" -ne $value) {
                $optionSyntax += " <$value>"
            }

            $requiredText = ""
            if ("true" -eq $required) {
                $requiredText = "Yes"
            } else {
                $requiredText = "No"
            }

            # Escape pipes and line breaks in descriptions
            $paramDescription = $paramDescription -replace '\|', '\|'
            $paramDescription = $paramDescription -replace '\r?\n', ' '

            Write-Output "| $optionSyntax | $requiredText | $paramDescription |"
        }
        Write-Output ""
    }

    # Process subcommands
    if ($null -ne $CommandNode.Command) {
        foreach ($subcommand in $CommandNode.Command) {
            Process-Command -CommandNode $subcommand -CommandPath $fullCommandPath -Level ($Level + 1)
        }
    }
}

# Start processing from the root commands
if ($null -ne $xmlDoc.Model.Command) {
    foreach ($command in $xmlDoc.Model.Command) {
        Process-Command -CommandNode $command
    }
} else {
    Write-Error "No commands found in the XML file."
}
