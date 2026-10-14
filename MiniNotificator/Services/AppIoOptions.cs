namespace MiniNotificator.Services;

/// <summary>
/// Configurazione del client AppIO, popolata dalla sezione "AppIo" di appsettings.json.
/// </summary>
public class AppIoOptions
{
    /// <summary>Chiave di sottoscrizione (Ocp-Apim-Subscription-Key) del servizio AppIO.</summary>
    public string AppKey { get; set; } = "";

    /// <summary>Endpoint base delle API IO, es. https://api.io.italia.it/api/v1</summary>
    public string Endpoint { get; set; } = "https://api.io.italia.it/api/v1";

    /// <summary>
    /// Endpoint base delle API "Manage" (back-office servizi), usato per leggere
    /// i metadati del servizio (ente, nome servizio). Es. https://api.io.pagopa.it/api/v1/manage
    /// </summary>
    public string ManageEndpoint { get; set; } = "https://api.io.pagopa.it/api/v1/manage";

    /// <summary>
    /// Chiave "Manage" (diversa dalla AppKey dei messaggi) necessaria per leggere i servizi.
    /// Se vuota, il banner informativo del servizio non viene mostrato.
    /// </summary>
    public string ManageKey { get; set; } = "";
}
