namespace bulkban.Wdac;

internal static class AllowAllTemplate
{
    internal const string EmbeddedXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <SiPolicy xmlns="urn:schemas-microsoft-com:sipolicy" PolicyType="Base Policy">
          <VersionEx>1.0.3.0</VersionEx>
          <PolicyID>{31351756-3F24-4963-8380-4E7602335AAE}</PolicyID>
          <BasePolicyID>{31351756-3F24-4963-8380-4E7602335AAE}</BasePolicyID>
          <PlatformID>{2E07F7E4-194C-4D20-B7C9-6F44A6C5A234}</PlatformID>
          <Rules>
            <Rule>
              <Option>Enabled:Unsigned System Integrity Policy</Option>
            </Rule>
            <Rule>
              <Option>Enabled:Advanced Boot Options Menu</Option>
            </Rule>
            <Rule>
              <Option>Enabled:UMCI</Option>
            </Rule>
            <Rule>
              <Option>Enabled:Update Policy No Reboot</Option>
            </Rule>
            <Rule>
              <Option>Disabled:Script Enforcement</Option>
            </Rule>
          </Rules>
          <EKUs />
          <FileRules>
            <Allow ID="ID_ALLOW_A_1" FileName="*" />
            <Allow ID="ID_ALLOW_A_2" FileName="*" />
          </FileRules>
          <Signers />
          <SigningScenarios>
            <SigningScenario Value="131" ID="ID_SIGNINGSCENARIO_DRIVERS_1" FriendlyName="AllowAll">
              <ProductSigners>
                <FileRulesRef>
                  <FileRuleRef RuleID="ID_ALLOW_A_1" />
                </FileRulesRef>
              </ProductSigners>
            </SigningScenario>
            <SigningScenario Value="12" ID="ID_SIGNINGSCENARIO_WINDOWS" FriendlyName="AllowAll">
              <ProductSigners>
                <FileRulesRef>
                  <FileRuleRef RuleID="ID_ALLOW_A_2" />
                </FileRulesRef>
              </ProductSigners>
            </SigningScenario>
          </SigningScenarios>
          <UpdatePolicySigners />
          <CiSigners />
          <HvciOptions>0</HvciOptions>
          <Settings>
            <Setting Provider="PolicyInfo" Key="Information" ValueName="Name">
              <Value>
                <String>AllowAll</String>
              </Value>
            </Setting>
            <Setting Provider="PolicyInfo" Key="Information" ValueName="Id">
              <Value>
                <String>022422</String>
              </Value>
            </Setting>
          </Settings>
        </SiPolicy>
        """;

    /// <summary>写入 AllowAll 模板;优先使用系统自带文件,缺失时写内嵌副本。</summary>
    internal static void WriteTo(string path)
    {
        var systemTemplate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "schemas",
            "CodeIntegrity",
            "ExamplePolicies",
            "AllowAll.xml");

        if (File.Exists(systemTemplate))
        {
            File.Copy(systemTemplate, path, overwrite: true);
            return;
        }

        File.WriteAllText(path, EmbeddedXml);
    }
}
