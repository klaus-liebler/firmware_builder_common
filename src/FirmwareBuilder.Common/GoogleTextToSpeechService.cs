using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FirmwareBuilder.Common;

public sealed record SpeechSentence(string Name, string Ssml);

// Sprachausgabe per Google Cloud Text-to-Speech (REST), Nachfolger von text_to_speech.ts. Anmeldung
// per Service-Account-JSON (dieselbe Datei, die zuvor ueber GOOGLE_APPLICATION_CREDENTIALS verwendet
// wurde): JWT (RS256) -> OAuth2-Access-Token -> text:synthesize. Ohne NuGet-Abhaengigkeiten.
// Bereits vorhandene mp3-Dateien werden nicht erneut erzeugt (kostet sonst Geld/Zeit).
public static class GoogleTextToSpeechService
{
	private const string Scope = "https://www.googleapis.com/auth/cloud-platform";
	private const string SynthesizeUrl = "https://texttospeech.googleapis.com/v1/text:synthesize";

	public static void SynthesizeMissing(
		string serviceAccountJsonPath,
		IEnumerable<SpeechSentence> sentences,
		string targetDirectory,
		string voiceName = "de-DE-Neural2-F",
		string languageCode = "de-DE",
		int sampleRateHertz = 22050)
	{
		var missing = sentences.Where(s => !File.Exists(Path.Combine(targetDirectory, s.Name + ".mp3"))).ToList();
		if (missing.Count == 0)
		{
			Console.WriteLine($"Alle Sprachdateien in {targetDirectory} sind bereits vorhanden.");
			return;
		}
		if (!File.Exists(serviceAccountJsonPath))
		{
			throw new FileNotFoundException($"Google-Service-Account-Datei fehlt: {serviceAccountJsonPath}");
		}

		using var http = new HttpClient();
		var token = GetAccessToken(http, serviceAccountJsonPath);
		Directory.CreateDirectory(targetDirectory);
		foreach (var s in missing)
		{
			var body = JsonSerializer.Serialize(new
			{
				input = new { ssml = s.Ssml },
				voice = new { name = voiceName, languageCode },
				audioConfig = new { audioEncoding = "MP3", sampleRateHertz },
			});
			using var req = new HttpRequestMessage(HttpMethod.Post, SynthesizeUrl)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			};
			req.Headers.Authorization = new("Bearer", token);
			using var resp = http.Send(req);
			var text = new StreamReader(resp.Content.ReadAsStream()).ReadToEnd();
			if (!resp.IsSuccessStatusCode)
			{
				throw new InvalidOperationException($"Google TTS lieferte HTTP {(int)resp.StatusCode} fuer \"{s.Name}\": {text}");
			}
			using var doc = JsonDocument.Parse(text);
			var audio = Convert.FromBase64String(doc.RootElement.GetProperty("audioContent").GetString()!);
			var path = Path.Combine(targetDirectory, s.Name + ".mp3");
			File.WriteAllBytes(path, audio);
			Console.WriteLine($"Sprachdatei {path} erzeugt ({audio.Length} Bytes): {s.Ssml}");
		}
	}

	private static string GetAccessToken(HttpClient http, string serviceAccountJsonPath)
	{
		using var doc = JsonDocument.Parse(File.ReadAllText(serviceAccountJsonPath));
		var root = doc.RootElement;
		var clientEmail = root.GetProperty("client_email").GetString()!;
		var tokenUri = root.GetProperty("token_uri").GetString()!;
		using var rsa = RSA.Create();
		rsa.ImportFromPem(root.GetProperty("private_key").GetString()!);

		static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var header = B64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
		var claims = B64Url(JsonSerializer.SerializeToUtf8Bytes(new { iss = clientEmail, scope = Scope, aud = tokenUri, iat = now, exp = now + 3600 }));
		var unsigned = header + "." + claims;
		var jwt = unsigned + "." + B64Url(rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

		using var resp = http.PostAsync(tokenUri, new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
			["assertion"] = jwt,
		})).GetAwaiter().GetResult();
		var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (!resp.IsSuccessStatusCode)
		{
			throw new InvalidOperationException($"Google-OAuth-Token-Abruf fehlgeschlagen (HTTP {(int)resp.StatusCode}): {text}");
		}
		using var tok = JsonDocument.Parse(text);
		return tok.RootElement.GetProperty("access_token").GetString()!;
	}
}
