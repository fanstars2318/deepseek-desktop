namespace DeepSeekBrowser.Services.Harness.Graph;

public static class HarnessGraphDefinitionParser
{
    public static HarnessGraphDefinition ParseFile(string path) => HarnessGraphParser.ParseFile(path);
    public static HarnessGraphDefinition ParseJson(string json) => HarnessGraphParser.ParseJson(json);
    public static HarnessGraphDefinition ParseYaml(string yaml) => HarnessGraphParser.ParseYaml(yaml);
}
