using System.Text;

namespace ParishRota.Functions.Tests;

public class MetaSignatureTests
{
    // RFC 4231 test case 2, chosen because both the key and the message are
    // plain ASCII — the same shape as a Meta app secret and a JSON body. The
    // digest is quoted from the RFC rather than computed here, so these tests
    // can disagree with the implementation.
    private const string Secret = "Jefe";
    private const string Body = "what do ya want for nothing?";
    private const string Digest = "5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843";

    private static byte[] BodyBytes => Encoding.UTF8.GetBytes(Body);

    [Fact]
    public void Accepts_a_signature_matching_the_body_and_secret()
    {
        Assert.True(MetaSignature.IsValid(BodyBytes, $"sha256={Digest}", Secret));
    }

    [Fact]
    public void Accepts_uppercase_hex()
    {
        // Meta sends lowercase, but nothing in the spec promises it will stay
        // that way, and a case-sensitive compare would fail closed on valid traffic.
        Assert.True(MetaSignature.IsValid(BodyBytes, $"sha256={Digest.ToUpperInvariant()}", Secret));
    }

    [Fact]
    public void Rejects_a_body_altered_after_signing()
    {
        var tampered = Encoding.UTF8.GetBytes(Body.Replace("nothing", "everything"));

        Assert.False(MetaSignature.IsValid(tampered, $"sha256={Digest}", Secret));
    }

    [Fact]
    public void Rejects_a_single_flipped_byte()
    {
        var tampered = BodyBytes;
        tampered[0] ^= 0x01;

        Assert.False(MetaSignature.IsValid(tampered, $"sha256={Digest}", Secret));
    }

    [Fact]
    public void Rejects_a_signature_made_with_a_different_secret()
    {
        Assert.False(MetaSignature.IsValid(BodyBytes, $"sha256={Digest}", "not-the-app-secret"));
    }

    [Theory]
    [InlineData(null)]                       // header absent entirely
    [InlineData("")]                         // header present but empty
    [InlineData("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843")] // no prefix
    [InlineData("sha1=5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843")]
    [InlineData("sha256=")]                  // prefix only
    [InlineData("sha256=5bdc")]              // truncated digest
    [InlineData("sha256=zzzz146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843")] // not hex
    public void Rejects_a_malformed_header(string? header)
    {
        Assert.False(MetaSignature.IsValid(BodyBytes, header, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Rejects_everything_when_no_app_secret_is_configured(string? secret)
    {
        // Fail closed. An unset secret must never degrade into "allow all".
        Assert.False(MetaSignature.IsValid(BodyBytes, $"sha256={Digest}", secret));
    }

    [Fact]
    public void Verifies_an_empty_body()
    {
        // `printf '' | openssl dgst -sha256 -hmac Jefe`. Meta will not send an
        // empty POST, but a zero-length stream must verify or reject on its
        // merits rather than throw.
        const string emptyBodyDigest = "923598ca6d64af2a5dba79dcd021a8a0fe5c5f557519adaaf0ad532d4506dd30";

        Assert.True(MetaSignature.IsValid(ReadOnlySpan<byte>.Empty, $"sha256={emptyBodyDigest}", Secret));
        Assert.False(MetaSignature.IsValid(ReadOnlySpan<byte>.Empty, $"sha256={Digest}", Secret));
    }
}
