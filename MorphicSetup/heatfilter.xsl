<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0" exclude-result-prefixes="xsl wix"
                xmlns:wix="http://wixtoolset.org/schemas/v4/wxs"
                xmlns="http://wixtoolset.org/schemas/v4/wxs">

  <xsl:output method="xml" indent="yes" omit-xml-declaration="yes" />

  <xsl:strip-space elements="*" />

  <xsl:key name="FilterPdbs" match="wix:Component[substring(wix:File/@Source, string-length(wix:File/@Source) - 3) = '.pdb']" use="@Id" />
  <xsl:key name="FilterMorphicExe" match="wix:Component[wix:File/@Source = 'SourceDir\Morphic.exe']" use="@Id" />

  <!-- Copy all elements and their attributes. -->
  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!-- Except for those that match our filters, do nothing. -->
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterPdbs', @Id ) ]" />
  <xsl:template match="*[ self::wix:Component or self::wix:ComponentRef ][ key( 'FilterMorphicExe', @Id ) ]" />

  <!-- Make every heat-harvested File a CompanionFile of morphic_exe. The MSI
       rule for companion files: a companion's install decision is inherited
       from its companion, bypassing the per-component "Disallowing installation
       of component since the same component with higher versioned keyfile
       exists" rule. That rule normally fires when a new file's baked-in
       version is lower than the installed copy on disk (but some files that we
       install--including Microsoft DLLs--can sometimes be older versions).

       morphic.exe's component is excluded from heat by the FilterMorphicExe
       key above and is declared manually in Package.wxs, so this template
       never touches the companion target itself. As long as morphic.exe's
       version monotonically increases each release, the version comparison
       on morphic.exe always wins and every companion installs alongside it,
       regardless of any baked-in file version on either side. -->
  <xsl:template match="wix:File">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <xsl:attribute name="CompanionFile">morphic_exe</xsl:attribute>
      <xsl:apply-templates select="node()" />
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>
<!-- adapted from WiX toolset sample (github.com/DeploymentDojo/BeltTest) -->
