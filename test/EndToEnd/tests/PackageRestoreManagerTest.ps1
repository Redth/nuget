# Tests for Package Restore feature

function Test-EnablePackageRestoreOnlyModifyProjectsThatHaveInstalledPackages
{
    param($context)

    # Arrange
    $p1 = New-ClassLibrary
    $p2 = New-ConsoleApplication

    $p1 | Install-Package jQuery -Source $context.RepositoryPath
    $p1.Save()
    $p2.Save()

    # Act
    Enable-PackageRestore

    # Assert
    Assert-AreEqual "true" (Get-MsBuildPropertyValue $p1 "RestorePackages")
    Assert-Null (Get-MsBuildPropertyValue $p2 "RestorePackages")
}

function Test-EnablePackageRestoreModifyProjectsWhenThePackagesFolderIsMissing
{
    param($context)

    # Arrange
    $p1 = New-ClassLibrary
    $p2 = New-ConsoleApplication

    $p1 | Install-Package jQuery -Source $context.RepositoryPath
    $p1.Save()
    $p2.Save()

    $packagesDir = Get-PackagesDir
    Assert-True (Test-Path $packagesDir)
    
    Remove-Item $packagesDir -Recurse
    Assert-False (Test-Path $packagesDir)

    # Act
    Enable-PackageRestore

    # Assert
    Assert-AreEqual "true" (Get-MsBuildPropertyValue $p1 "RestorePackages")
    Assert-Null (Get-MsBuildPropertyValue $p2 "RestorePackages")
}

function Test-EnablePackageRestoreModifyProjectThatInstallNewPackages
{
    param($context)

    # Arrange
    $p = New-ClassLibrary
    Enable-PackageRestore

    Assert-Null (Get-MsBuildPropertyValue $p "RestorePackages")

    # Act
    $p | Install-Package jQuery -Source $context.RepositoryPath

    # Assert
    Assert-AreEqual "true" (Get-MsBuildPropertyValue $p "RestorePackages")
}

function NoTest-EnablePackageRestoreOnCspProjects
{
    if ($dte.Version -eq "10.0")
    {
        return
    }

    # Arrange
    $p1 = New-JavaScriptApplication
    $p2 = New-NativeWinStoreApplication

    Install-Package jQuery -ProjectName $p1.Name

    Install-Package zlib -ProjectName $p2.Name
    $p2.Save()

    # Act
    Enable-PackageRestore

    # Assert
    Assert-AreEqual "true" (Get-MsBuildPropertyValue $p1 "RestorePackages")
    Assert-AreEqual "true" (Get-MsBuildPropertyValue $p2 "RestorePackages")
}