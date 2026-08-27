namespace MarketOtomasyon.Tests;

public sealed class DockerGuvenligiTests
{
    private static string DockerDosyasi(string ad) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docker", ad));

    [Fact]
    public void Runtime_RootKullaniciylaCalismamali()
    {
        var dockerfile = DockerDosyasi("Dockerfile");

        Assert.Contains("USER app", dockerfile);
        Assert.True(
            dockerfile.IndexOf("USER app", StringComparison.Ordinal) <
            dockerfile.IndexOf("ENTRYPOINT", StringComparison.Ordinal));
    }

    [Fact]
    public void Dockerfile_CanlilikKontroluVeAcikPortIcerir()
    {
        var dockerfile = DockerDosyasi("Dockerfile");

        Assert.Contains("EXPOSE 8080", dockerfile);
        Assert.Contains("HEALTHCHECK", dockerfile);
        Assert.Contains("healthcheck", dockerfile);
    }

    [Theory]
    [InlineData("/var/lib/marketotomasyon/keys")]
    [InlineData("/App/wwwroot/urun-resim")]
    [InlineData("/App/Loglar")]
    public void Dockerfile_UretilenDosyalariKaliciDepolamayaAyirir(string yol)
    {
        var dockerfile = DockerDosyasi("Dockerfile");

        Assert.Contains(yol, dockerfile);
        Assert.Contains("VOLUME", dockerfile);
    }

    [Theory]
    [InlineData(".git/")]
    [InlineData(".env")]
    [InlineData("**/appsettings.Development.json")]
    [InlineData("**/bin/")]
    [InlineData("**/obj/")]
    [InlineData("**/Loglar/")]
    public void BuildContext_YerelVeHassasDosyalariDisaridaBirakir(string kural)
    {
        var dockerignore = DockerDosyasi(".dockerignore");

        Assert.Contains(kural, dockerignore);
    }
}
