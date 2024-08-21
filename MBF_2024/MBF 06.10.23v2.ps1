$manufacturer = "asus"
$subcategory = "motherboards-components"
$category = "motherboards"

# Überprüfe, ob der Computermodellname "HP" enthält, und wechsle zur HP-Website, wenn er vorhanden ist.
$computerModel = (Get-WmiObject -Class Win32_ComputerSystem).Model

if ($computerModel -match "HP") {
    # HP-Modell erkannt, Code für HP-Modelle hier einfügen
    Start-Process "https://support.hp.com/ch-de/drivers/products"
    Write-Host "Das Skript wird für 1 Sekunde pausiert ..."
    Start-Sleep -Seconds 2

    # Zeige die Seriennummer in einer MessageBox an und kopiere sie bei OK-Klick
    Add-Type -AssemblyName System.Windows.Forms

    $serialNumber = (Get-WmiObject -Class Win32_BIOS).SerialNumber

    $buttonClicked = [System.Windows.Forms.MessageBox]::Show("<OK> Drücken um die Seriennummer zu kopieren", "Seriennummer", [System.Windows.Forms.MessageBoxButtons]::OKCancel, [System.Windows.Forms.MessageBoxIcon]::Information)

    if ($buttonClicked -eq [System.Windows.Forms.DialogResult]::OK) {
        $serialNumber | Set-Clipboard
    }
    if ($buttonClicked -eq [System.Windows.Forms.DialogResult]::Cancel) {
    exit
    }
    if ($confirmation -eq "Abbrechen") {
    exit
    
    }
} elseif ($computerModel -match "lenovo|thinkpad|thinkcenter|thinkstation|yoga|thinkbook|ideapad|legion|loq Gaming|chromebook|ideacentre") {
    # Lenovo-Modell erkannt, Code für Lenovo-Modelle hier einfügen
    $manufacturer = "pcsupport.lenovo"

    # Je nachdem, ob es sich um ein Laptop/Notebook oder einen Desktop handelt, verwenden Sie die entsprechende URL
    if ($computerModel -match "thinkpad|yoga-series|thinkbook|ideapad|laptop|chromebook|legion-series|gaming-series") {
        $lenovoUrl = "products/laptops-and-netbooks/"
    } elseif ($computerModel -match "desktop|yoga-aio|ideacenter|thinkcenter|legion-aio|thinkstaion") {
        $lenovoUrl = "products/desktops-and-all-in-ones/"
    }

    # Weitere Verarbeitung für Lenovo-Modelle
    if ($lenovoUrl -ne "") {
        # Überprüfen Sie, ob das Modell auf der Lenovo-Website existiert.
        if (Test-MainboardModelExists "https://$manufacturer.com/ch-de/$lenovoUrl") {
            # Modell auf der Lenovo-Website gefunden
            $driverOverviewUrl = "https://$manufacturer.com/ch/en/$lenovoUrl"
            Start microsoft-edge:$driverOverviewUrl
            Write-Host "Das Skript wird für 1 Sekunde pausiert ..."
    Start-Sleep -Seconds 2

    # Zeige die Seriennummer in einer MessageBox an und kopiere sie bei OK-Klick
    Add-Type -AssemblyName System.Windows.Forms

    $serialNumber = (Get-WmiObject -Class Win32_BIOS).SerialNumber

    $buttonClicked = [System.Windows.Forms.MessageBox]::Show("<OK> Drücken um die Seriennummer zu kopieren", "Seriennummer", [System.Windows.Forms.MessageBoxButtons]::OKCancel, [System.Windows.Forms.MessageBoxIcon]::Information)

    if ($buttonClicked -eq [System.Windows.Forms.DialogResult]::OK) {
        $serialNumber | Set-Clipboard
    }
    if ($buttonClicked -eq [System.Windows.Forms.DialogResult]::Cancel) {
    exit
    }
        } 
        else {
            # Modell nicht auf der Lenovo-Website gefunden
        }
    }
}

    # Wenn weder HP noch Lenovo erkannt wurden, ermitteln und bereinigen Sie den Motherboard-Namen.
    $Motherboard = Get-ItemProperty "HKLM:\HARDWARE\DESCRIPTION\System\BIOS" | Select-Object -ExpandProperty BaseBoardProduct
    $Motherboard = $Motherboard.ToUpper()
    $Motherboard = $Motherboard -replace '\(.*\)', ''
    $Motherboard = $Motherboard -replace '\([^)]*\)', ''
    $Motherboard = $Motherboard -replace '[^\w\d]', '-'
    $Motherboard = $Motherboard.TrimEnd('-')

    $installedModel = $Motherboard
    $installedModel = $installedModel.ToUpper()
    $installedModel = $installedModel -replace '\(.*\)', ''
    $installedModel = $installedModel -replace '\([^)]*\)', ''
    $installedModel = $installedModel -replace '[^\w\d]', '-'
    $installedModel = $installedModel.TrimEnd('-')

    $patternTuf = "TUF"
    $patternPrime = "PRIME"
    $patternROG = "ROG"
    $patternART = "ART"
    $patternWOST = "WORKSTATION"
    $patternBUI = "BUISNESS"
    $patternEXP = "EXPEDITION"
    $patternOTH = "OTHERS"

    # Überprüfen Sie, zu welcher Serie das Motherboard gehört.
    if ($installedModel -like "*TUF*") {
        $series = "tuf-gaming"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*PRIME*") {
        $series = "prime"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*ROG*") {
        $series = "rog-republic-of-gamers"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*ART*") {
        $series = "proart"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*WORKSTATION*") {
        $series = "workstation"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*BUISNESS*") {
        $series = "business"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*EXPEDITION*") {
        $series = "expedition"
        $manufacturer = "asus"
    } elseif ($installedModel -like "*OTHERS*") {
        $series = "others"
        $manufacturer = "asus"
    } else {
        $manufacturer = "msi"
        $series = $installedModel  # Als MSI-Serie verwenden, wenn keine ASUS-Serie erkannt wird
    }

    $cleanedMotherboardName = $Motherboard -replace '\(.*\)', ''
    $Motherboard = $Motherboard -replace '\s', '-'
    $installedModel = $installedModel -replace '\s', '-'
    $subcategory = $subcategory -replace '\s', '-'
    $category = $category -replace '\s', '-'
    $series = $series -replace '\s', '-'
    $cleanedMotherboardName = $cleanedMotherboardName -replace '\s', ''

    # Ermitteln Sie den CPU-Herstellernamen (Intel oder AMD).
    $cpuInfo = Get-WmiObject -Class Win32_Processor
    $cpuName = $cpuInfo.Name

    # Initialisieren Sie den Herstellernamen als "Unbekannt" (Standardwert)
    $cpuManufacturer = "Unbekannt"

    # Überprüfen Sie, ob der CPU-Name "Intel" enthält
    if ($cpuName -match "Intel") {
        $cpuManufacturer = "Intel"
    } elseif ($cpuName -match "AMD") {
        $cpuManufacturer = "AMD"
    }

    # Ausgabe des Herstellernamens (ohne zusätzlichen Text)

    # Überprüfen Sie, ob das Modell auf der ASUS-Website existiert.
    $asusUrl = "https://www.$manufacturer.com/ch-de/$subcategory/$category/$series/"

    # Funktion zum Überprüfen, ob ein Mainboard-Modell auf einer Website gefunden wird
function Test-MainboardModelExists($url, $model) {
    $response = Invoke-WebRequest -Uri "$url$model" -ErrorAction SilentlyContinue
    return $response.StatusCode -eq 200
}
    
    if (Test-MainboardModelExists $asusUrl $cleanedMotherboardName) {
        # Modell auf der ASUS-Website gefunden
        $driverOverviewUrl = "https://www.$manufacturer.com/ch-de/$subcategory/$category/$series/$installedModel/helpdesk_download/?model2Name=$installedModel"
        Start microsoft-edge:$driverOverviewUrl
        exit
    } else {
        # Modell nicht auf der ASUS-Website gefunden
        # Wechseln Sie zur MSI-Website und suchen Sie dort
        $manufacturer = "msi"
        $msiUrl = "https://www.$manufacturer.com/Motherboard/"
        
        if (Test-MainboardModelExists $msiUrl $installedModel) {
            # Modell auf der MSI-Website gefunden
            $driverOverviewUrl = "https://www.$manufacturer.com/Motherboard/$installedModel/support#driver"
            Start microsoft-edge:$driverOverviewUrl
            exit
        } else {
            # Modell nicht auf der MSI-Website gefunden
            # Wechseln zu Gigabyte und suchen Sie dort
            $manufacturer = "gigabyte"
            $gigabyteUrl = "https://www.$manufacturer.com/Motherboard/"
            
            if (Test-MainboardModelExists $gigabyteUrl $installedModel) {
                # Modell auf der Gigabyte-Website gefunden
                $driverOverviewUrl = "https://www.$manufacturer.com/Motherboard/$installedModel/support"
                Start microsoft-edge:$driverOverviewUrl
                exit
            } else {
                # Modell nicht auf der Gigabyte-Website gefunden
                # Wechseln zu ASRock und suchen Sie dort
                $manufacturer = "asrock"
                $asrockUrl = "https://www.$manufacturer.com/mb/"
                
                # Fügen Sie den CPU-Herstellernamen zur ASRock-URL hinzu
                if ($cpuManufacturer -eq "Intel") {
                    $asrockUrl += "Intel/"
                } elseif ($cpuManufacturer -eq "AMD") {
                    $asrockUrl += "AMD/"
                }
                
                $asrockUrl += "$installedModel/index.de.asp#download"
                $asrockUrl = $asrockUrl -replace "-", " "
                
                if (Test-MainboardModelExists $asrockUrl $installedModel) {
                    # Modell auf der ASRock-Website gefunden
                    $driverOverviewUrl = $asrockUrl
                    Start microsoft-edge:$driverOverviewUrl
                    exit
                } else {
                    # Modell nicht auf einer der unterstützten Websites gefunden.
                }
            }
        }
    }

Write-Host "URL zur Treiberübersichtsseite: $driverOverviewUrl"