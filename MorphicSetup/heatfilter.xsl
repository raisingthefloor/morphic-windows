<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0" exclude-result-prefixes="xsl wix"
                xmlns:wix="http://wixtoolset.org/schemas/v4/wxs"
                xmlns="http://wixtoolset.org/schemas/v4/wxs">

  <xsl:output method="xml" indent="yes" omit-xml-declaration="yes" />

  <xsl:strip-space elements="*" />

  <xsl:key name="FilterPdbs" match="wix:Component[substring(wix:File/@Source, string-length(wix:File/@Source) - 3) = '.pdb']" use="@Id" />
  <xsl:key name="FilterMorphicExe" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.exe']" use="@Id" />

  <!-- Exclude Morphic.Core.dll and Microsoft.DiaSymReader.Native.amd64.dll from heat
       so we can manually author them inside morphic.exe's Component in Package.wxs
       with CompanionFile="morphic_exe". Both files are app-local private DLLs that
       always ship with Morphic.exe, so grouping them in the same Component is a
       reasonable exception to the one-file-per-component guideline.

       Why we need this exception: the OLD installer (v1.x) shipped these two files
       at higher baked-in versions than v2.x does). MSI's default file-versioning
       rule fires "Disallowing installation of component since the same component
       with higher versioned keyfile exists" at CostFinalize, which is before
       RemoveExistingProducts runs and can't be reordered around it. Moving these
       files into morphic.exe's Component plus marking them CompanionFile means
       their install decision is inherited from morphic.exe (whose version always
       increases each release), bypassing the per-file version comparison entirely.

       If a future build accidentally regresses some other file's version, the
       "Disallowing installation of component" message in the install log will
       identify it and it can be added to the same exclude+companion pattern. -->
  <xsl:key name="FilterMorphicCoreDll" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.Core.dll']" use="@Id" />

  <!-- Architecture-specific DiaSymReader filters. Only one of these matches anything
       per build: the .amd64 filename exists only in x64 publish output, the .arm64
       filename only in arm64 publish output. The non-matching filter is a harmless
       no-op. Both are paired with the $(sys.BUILDARCH)-conditional File declarations
       in Package.wxs that manually add the architecture-matched variant to
       MorphicExeComponent as a CompanionFile of morphic_exe. -->
  <xsl:key name="FilterDiaSymReaderNativeAmd64Dll" match="wix:Component[wix:File/@Source = 'SourceDir\Microsoft.DiaSymReader.Native.amd64.dll']" use="@Id" />
  <xsl:key name="FilterDiaSymReaderNativeArm64Dll" match="wix:Component[wix:File/@Source = 'SourceDir\Microsoft.DiaSymReader.Native.arm64.dll']" use="@Id" />

  <!-- Exclude Morphic.NotificationHelper.* (exe, dll, deps.json, runtimeconfig.json, pri)
       from heat so we can declare them manually in Package.wxs's NotificationHelperComponent
       with stable File IDs. The UnregisterNotificationHelper custom action references
       notification_helper_exe by File ID; heat-generated IDs are not guaranteed stable
       across builds, so we need the manual declaration. The .pdb is already covered by
       the universal FilterPdbs key above. -->
  <xsl:key name="FilterNotificationHelperExe" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.NotificationHelper.exe']" use="@Id" />
  <xsl:key name="FilterNotificationHelperDll" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.NotificationHelper.dll']" use="@Id" />
  <xsl:key name="FilterNotificationHelperDepsJson" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.NotificationHelper.deps.json']" use="@Id" />
  <xsl:key name="FilterNotificationHelperRuntimeConfigJson" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.NotificationHelper.runtimeconfig.json']" use="@Id" />
  <xsl:key name="FilterNotificationHelperPri" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.NotificationHelper.pri']" use="@Id" />

  <!-- Copy all elements and their attributes. -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!-- Except for those that match our filters, do nothing. -->
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterPdbs', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterMorphicExe', @Id ) ]" />
  <!-- Filter out notification helpers -->
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterNotificationHelperExe', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterNotificationHelperDll', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterNotificationHelperDepsJson', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterNotificationHelperRuntimeConfigJson', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterNotificationHelperPri', @Id ) ]" />
  <!-- Additionally, filter out binraies that should always be replaced (even if it's a "downgrade") -->
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterMorphicCoreDll', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterDiaSymReaderNativeAmd64Dll', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterDiaSymReaderNativeArm64Dll', @Id ) ]" />
</xsl:stylesheet>
<!-- adapted from WiX toolset sample (github.com/DeploymentDojo/BeltTest) -->
