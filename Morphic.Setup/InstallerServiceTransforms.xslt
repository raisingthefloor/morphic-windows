<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
            xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:msxsl="urn:schemas-microsoft-com:xslt"
            exclude-result-prefixes="msxsl"
            xmlns:wix="http://schemas.microsoft.com/wix/2006/wi">
  
  <xsl:output method="xml" indent="yes"/>
  
  <xsl:strip-space elements="*"/>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
  </xsl:template>
  
  <xsl:template match="wix:Component[wix:File[@Source='$(var.InstallerServiceOutputDirectory)\Morphic.InstallerService.exe']]">
    <xsl:copy>
      <xsl:apply-templates select="node() | @*" />
      <wix:ServiceInstall Id="InstallerServiceInstall" DisplayName="Morphic IoD Service" Description="Install on Demand Service" Name="MorphicInstallerService" ErrorControl="ignore" Start="auto" Type="ownProcess" Vital="yes" Interactive="no" Account="LocalSystem" />
      <wix:ServiceControl Id="InstallerServiceControl" Name="MorphicInstallerService" Start="install" Stop="both" Remove="uninstall" Wait="yes"/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>