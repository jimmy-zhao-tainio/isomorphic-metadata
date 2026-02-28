param(
    [string]$Root = "Samples/MainWorkspace"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ModelPath {
    param([string]$InstancePath)

    $dir = Split-Path -Path $InstancePath -Parent
    while (-not [string]::IsNullOrWhiteSpace($dir)) {
        $metadataModel = Join-Path $dir "metadata/model.xml"
        if (Test-Path $metadataModel) {
            return (Resolve-Path $metadataModel).Path
        }

        $sampleModel = Join-Path $dir "SampleModel.xml"
        if (Test-Path $sampleModel) {
            return (Resolve-Path $sampleModel).Path
        }

        $localModel = Join-Path $dir "model.xml"
        if (Test-Path $localModel) {
            return (Resolve-Path $localModel).Path
        }

        $parent = Split-Path -Path $dir -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $dir) {
            break
        }

        $dir = $parent
    }

    return $null
}

function Get-EntityContract {
    param([string]$ModelPath)

    [xml]$modelXml = Get-Content -Path $ModelPath -Raw
    $map = @{}
    foreach ($entityNode in $modelXml.SelectNodes("/Model/Entities/Entity")) {
        $entityName = [string]$entityNode.GetAttribute("name")
        if ([string]::IsNullOrWhiteSpace($entityName)) {
            continue
        }

        $propertySet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($propertyNode in $entityNode.SelectNodes("./Properties/Property")) {
            $propertyName = [string]$propertyNode.GetAttribute("name")
            if ([string]::IsNullOrWhiteSpace($propertyName)) {
                continue
            }

            if ($propertyName -ieq "Id") {
                continue
            }

            [void]$propertySet.Add($propertyName)
        }

        $relationshipSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($relationshipNode in $entityNode.SelectNodes("./Relationships/Relationship")) {
            $targetEntity = [string]$relationshipNode.GetAttribute("entity")
            if ([string]::IsNullOrWhiteSpace($targetEntity)) {
                continue
            }

            [void]$relationshipSet.Add($targetEntity)
        }

        $map[$entityName] = @{
            Properties = $propertySet
            Relationships = $relationshipSet
        }
    }

    return $map
}

function Convert-InstanceFile {
    param(
        [string]$InstancePath,
        [string]$ModelPath
    )

    $entityContract = Get-EntityContract -ModelPath $ModelPath
    [xml]$doc = Get-Content -Path $InstancePath -Raw

    $root = $doc.DocumentElement
    if ($null -eq $root) {
        return $false
    }

    $changed = $false
    foreach ($listNode in @($root.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })) {
        $listName = [string]$listNode.Name
        $entityNameFromList = if ($listName.EndsWith("List", [System.StringComparison]::OrdinalIgnoreCase)) {
            $listName.Substring(0, $listName.Length - 4)
        }
        else {
            $listName
        }

        foreach ($rowNode in @($listNode.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })) {
            $rowEntityName = [string]$rowNode.Name
            $entityName = if ($entityContract.ContainsKey($rowEntityName)) { $rowEntityName } else { $entityNameFromList }
            if (-not $entityContract.ContainsKey($entityName)) {
                continue
            }

            $contract = $entityContract[$entityName]
            $properties = $contract.Properties
            $relationships = $contract.Relationships

            $existingPropertyElements = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($childElement in @($rowNode.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })) {
                if (-not $relationships.Contains([string]$childElement.Name)) {
                    [void]$existingPropertyElements.Add([string]$childElement.Name)
                }
            }

            $idValue = [string]$rowNode.GetAttribute("Id")
            $sourceAttributes = @()
            foreach ($attribute in @($rowNode.Attributes)) {
                $sourceAttributes += @{ Name = [string]$attribute.Name; Value = [string]$attribute.Value }
            }

            while ($rowNode.Attributes.Count -gt 0) {
                [void]$rowNode.Attributes.RemoveAt(0)
                $changed = $true
            }

            if (-not [string]::IsNullOrWhiteSpace($idValue)) {
                [void]$rowNode.SetAttribute("Id", $idValue)
            }

            foreach ($attribute in $sourceAttributes) {
                $attributeName = [string]$attribute.Name
                if ($attributeName -ieq "Id") {
                    continue
                }

                if ($attributeName.EndsWith("Id", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $relationshipEntity = $attributeName.Substring(0, $attributeName.Length - 2)
                    if ($relationships.Contains($relationshipEntity)) {
                        [void]$rowNode.SetAttribute(($relationshipEntity + "Id"), [string]$attribute.Value)
                        continue
                    }
                }

                if ($properties.Contains($attributeName) -and -not $existingPropertyElements.Contains($attributeName)) {
                    $propertyElement = $doc.CreateElement($attributeName)
                    $propertyElement.InnerText = [string]$attribute.Value
                    [void]$rowNode.AppendChild($propertyElement)
                    [void]$existingPropertyElements.Add($attributeName)
                    $changed = $true
                }
            }

            $relationshipElementsToRemove = New-Object System.Collections.Generic.List[System.Xml.XmlElement]
            foreach ($childElement in @($rowNode.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })) {
                $childName = [string]$childElement.Name
                if (-not $relationships.Contains($childName)) {
                    continue
                }

                $relationshipId = [string]$childElement.GetAttribute("Id")
                if (-not [string]::IsNullOrWhiteSpace($relationshipId)) {
                    [void]$rowNode.SetAttribute(($childName + "Id"), $relationshipId)
                }

                $relationshipElementsToRemove.Add($childElement)
            }

            foreach ($relationshipElement in $relationshipElementsToRemove) {
                [void]$rowNode.RemoveChild($relationshipElement)
                $changed = $true
            }
        }
    }

    if ($changed) {
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Indent = $true
        $settings.OmitXmlDeclaration = $false
        $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
        $settings.NewLineChars = "`n"
        $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
        $settings.NewLineOnAttributes = $false

        $writer = [System.Xml.XmlWriter]::Create($InstancePath, $settings)
        try {
            $doc.Save($writer)
        }
        finally {
            $writer.Dispose()
        }
    }

    return $changed
}

$files = Get-ChildItem -Path $Root -Recurse -File -Filter *.xml |
Where-Object {
    $_.FullName -match '\\metadata\\instance\\[^\\]+\.xml$' -or
    $_.Name -ieq 'instance.xml' -or
    $_.Name -ieq 'SampleInstance.xml'
}

$converted = 0
foreach ($file in $files) {
    $modelPath = Resolve-ModelPath -InstancePath $file.FullName
    if ([string]::IsNullOrWhiteSpace($modelPath)) {
        continue
    }

    if (Convert-InstanceFile -InstancePath $file.FullName -ModelPath $modelPath) {
        $converted++
        Write-Host "Converted: $($file.FullName)"
    }
}

Write-Host "Converted files: $converted"

