using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MiniNotificator.Services;

/// <summary>
/// Client minimale per l'invio di messaggi tramite le API di AppIO.
/// Contratto API utilizzato:
///   POST {endpoint}/profiles            -> verifica che il destinatario abbia abilitato il servizio
///   POST {endpoint}/messages/{cf}       -> invia il messaggio
/// con header di autenticazione "Ocp-Apim-Subscription-Key".
/// </summary>
public class AppIoClient
{
    private readonly HttpClient _http;
    private readonly AppIoOptions _options;

    public AppIoClient(HttpClient http, IOptions<AppIoOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <summary>
    /// Verifica se il cittadino (codice fiscale) ha abilitato la ricezione dei messaggi dal servizio.
    /// </summary>
    public async Task<ProfileResult> GetProfileAsync(string fiscalCode)
    {
        var url = $"{_options.Endpoint}/profiles";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.AppKey);
        request.Content = JsonContent.Create(new { fiscal_code = fiscalCode });

        var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        bool senderAllowed = false;
        if (response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("sender_allowed", out var prop))
                    senderAllowed = prop.GetBoolean();
            }
            catch (JsonException) { /* risposta non JSON, lasciamo senderAllowed=false */ }
        }

        var trace = BuildTrace("POST", url, fiscalCode, response, body);
        return new ProfileResult(response.IsSuccessStatusCode, (int)response.StatusCode, senderAllowed, body, trace);
    }

    /// <summary>
    /// Invia un messaggio (oggetto + corpo markdown) al destinatario indicato dal codice fiscale.
    /// </summary>
    public async Task<SendResult> SendMessageAsync(string fiscalCode, string subject, string markdown)
    {
        var url = $"{_options.Endpoint}/messages/{fiscalCode}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.AppKey);

        var payload = new { content = new { subject, markdown } };
        var json = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        string? messageId = null;
        if (response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var prop))
                    messageId = prop.GetString();
            }
            catch (JsonException) { /* ignora */ }
        }

        var trace = BuildTrace("POST", url, fiscalCode, response, body);
        return new SendResult(response.IsSuccessStatusCode, (int)response.StatusCode, messageId, body, trace);
    }

    /// <summary>
    /// Legge i metadati del servizio collegato alla Manage key:
    /// ente (organizzazione), nome servizio, dipartimento. Usa l'API "Manage" (Get User Services).
    /// </summary>
    public async Task<ServiceInfo> GetServiceInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.ManageKey))
            return ServiceInfo.Unavailable("Manage key non configurata (AppIo:ManageKey).", Environment());

        var url = $"{_options.ManageEndpoint}/services";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ManageKey);

        try
        {
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return ServiceInfo.Unavailable($"Lettura servizio fallita (HTTP {(int)response.StatusCode}).", Environment(), body);

            using var doc = JsonDocument.Parse(body);
            var svc = FirstService(doc.RootElement);
            if (svc is null)
                return ServiceInfo.Unavailable("Nessun servizio trovato per questa Manage key.", Environment(), body);

            var service = svc.Value;
            var org = service.TryGetProperty("organization", out var o) ? o : default;

            return new ServiceInfo(
                Success: true,
                OrganizationName: GetStr(org, "name") ?? "(ente sconosciuto)",
                DepartmentName: GetStr(org, "department_name"),
                OrganizationFiscalCode: GetStr(org, "fiscal_code"),
                ServiceName: GetStr(service, "name") ?? "(servizio senza nome)",
                ServiceId: GetStr(service, "id"),
                Environment: Environment(),
                Error: null,
                RawBody: body);
        }
        catch (Exception ex)
        {
            return ServiceInfo.Unavailable($"Errore lettura servizio: {ex.Message}", Environment());
        }
    }

    /// <summary>Estrae il primo servizio dall'array di risposta, qualunque sia il nome del campo contenitore.</summary>
    private static JsonElement? FirstService(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.GetArrayLength() > 0 ? root[0] : null;

        foreach (var name in new[] { "value", "items", "services", "data" })
        {
            if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                return arr[0];
        }
        return null;
    }

    private static string? GetStr(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    /// <summary>Deduce l'ambiente dall'host dell'endpoint messaggi.</summary>
    private string Environment()
    {
        var ep = _options.Endpoint ?? "";
        if (ep.Contains("api.io.italia.it") || ep.Contains("api.io.pagopa.it"))
            return "PRODUZIONE";
        return $"NON-STANDARD ({ep})";
    }

    /// <summary>
    /// Costruisce una traccia diagnostica leggibile della chiamata (per UI di test).
    /// La subscription key viene mascherata: mai loggata in chiaro.
    /// </summary>
    private string BuildTrace(string method, string url, string fiscalCode, HttpResponseMessage response, string body)
    {
        var auth = response.IsSuccessStatusCode
            ? "key riconosciuta (HTTP 2xx)"
            : (int)response.StatusCode switch
            {
                401 => "KEY NON VALIDA / ASSENTE (401) -> non autenticato",
                403 => "key valida, ma destinatario/servizio non autorizzato (403)",
                400 => "key valida, richiesta raggiunta ma payload rifiutato (400)",
                404 => "key valida, risorsa non trovata (404)",
                _   => $"autenticato, esito HTTP {(int)response.StatusCode}",
            };

        var sb = new StringBuilder();
        sb.AppendLine($"{method} {url}");
        sb.AppendLine($"Ocp-Apim-Subscription-Key: {MaskKey(_options.AppKey)}");
        sb.AppendLine($"fiscal_code: {fiscalCode}");
        sb.AppendLine($"-> HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        sb.AppendLine($"-> Auth: {auth}");
        sb.AppendLine($"-> Body: {body}");
        return sb.ToString();
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "(vuota)";
        if (key.Length <= 8)
            return new string('*', key.Length);
        return $"{key[..4]}...{key[^4..]} (len {key.Length})";
    }
}

public record ProfileResult(bool Success, int StatusCode, bool SenderAllowed, string RawBody, string Trace);

public record SendResult(bool Success, int StatusCode, string? MessageId, string RawBody, string Trace);

/// <summary>Metadati del servizio mittente, per il banner informativo.</summary>
public record ServiceInfo(
    bool Success,
    string OrganizationName,
    string? DepartmentName,
    string? OrganizationFiscalCode,
    string ServiceName,
    string? ServiceId,
    string Environment,
    string? Error,
    string RawBody)
{
    public static ServiceInfo Unavailable(string error, string environment, string rawBody = "")
        => new(false, "", null, null, "", null, environment, error, rawBody);
}
