using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.Tooling;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Serilog.Log;

[GitHubActions("excuses",
    GitHubActionsImage.WindowsLatest,
    AutoGenerate = true,
    OnCronSchedule = "0 * * * *",
    CacheIncludePatterns = new[] { "~/.nuget/packages" },
    ImportSecrets = new[] { "NUKE_EXCUSES_GET_ENDPOINT", "NUKE_EXCUSES_REGEX" },
    InvokedTargets = new[] { nameof(NewExcuse) },
    OnWorkflowDispatchOptionalInputs = new[] { "DUMMY_NAME" }
    )]
class Build : NukeBuild
{
    const string ExcuseToken = "@@excuse@@";

    [GitRepository] readonly GitRepository GitRepository;

    [PathExecutable] readonly Tool Git;

    [Parameter] readonly string ExcusesGetEndpoint;
    [Parameter] readonly string ExcusesRegex;

    public static int Main() => Execute<Build>();

    Target NewExcuse => _ => _
        .Requires(() => ExcusesGetEndpoint, () => ExcusesRegex)
        .Executes(async () =>
        {
            Information("Getting a new excuse...");
            var excuse = await GetExcuse();
            Information("Getting content of README-template.md...");
            var template = await GetTemplateContent();
            Information("Generating new README.md from template...");
            var interpolatedContent = IntepolateTemplateWithExcuse(template, excuse, ExcuseToken);
            Information("Replacing old README.md...");
            await ReplaceReadme(interpolatedContent);
            Information("Commiting changes...");
            CommitChanges();
            Information("ALL DONE!");
        });

    private Task ReplaceReadme(string interpolatedContent) => File.WriteAllTextAsync(RootDirectory / "README.md", interpolatedContent, Encoding.UTF8);
    private string IntepolateTemplateWithExcuse(string template, string excuse, string excuseToken) => template.Replace(excuseToken, excuse);
    private Task<string> GetTemplateContent() => File.ReadAllTextAsync(RootDirectory / "README-template.md");
    private async Task<string> GetExcuse()
    {
        var client = new HttpClient();
        var response = await client.GetStringAsync(ExcusesGetEndpoint);

        var match = Regex.Match(response, ExcusesRegex);
        if (!match.Success) throw new Exception("Expected a match in regex but found none");
        var firstCapture = match.Groups.Values.Skip(1).FirstOrDefault()?.Value;
        if (firstCapture == null) throw new Exception("Expexted a first capture group to return a match but no match was found");

        return firstCapture;
    }
    private void CommitChanges()
    {
        Git("config --global user.name \"excuse bot [bot]\"");
        Git("config --global user.email \"denis.ekart@gmail.com\"");
        Git("add -A");
        Git("commit -m \"[bot] chore: another day, another excuse\"");
        Git("push origin HEAD:main");
    }
}
