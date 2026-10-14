using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MiniNotificator.Services;

namespace MiniNotificator.Pages;

public class IndexModel : PageModel
{
    private readonly AppIoClient _appIo;

    public IndexModel(AppIoClient appIo)
    {
        _appIo = appIo;
    }

    [BindProperty]
    public MessageInput Input { get; set; } = new();

    // Esito da mostrare dopo il POST
    public bool? Ok { get; set; }
    public string? StatusText { get; set; }
    public string? Details { get; set; }
    public string? Log { get; set; }

    // Banner: per conto di quale ente / servizio / ambiente stai inviando
    public ServiceInfo? Service { get; set; }

    public async Task OnGetAsync()
    {
        Service = await _appIo.GetServiceInfoAsync();
    }

    // Verifica se il destinatario ha abilitato il servizio (POST /profiles)
    public async Task<IActionResult> OnPostVerificaAsync()
    {
        Service = await _appIo.GetServiceInfoAsync();
        // per la verifica serve solo il codice fiscale
        ModelState.Remove(nameof(Input.Subject));
        ModelState.Remove(nameof(Input.Markdown));
        if (!TryValidateFiscalCode())
            return Page();

        var result = await _appIo.GetProfileAsync(Input.FiscalCode);
        Ok = result.Success && result.SenderAllowed;
        StatusText = result.Success
            ? (result.SenderAllowed
                ? "Destinatario raggiungibile: il servizio è abilitato."
                : "Il destinatario non ha abilitato questo servizio.")
            : $"Errore nella verifica (HTTP {result.StatusCode}).";
        Details = result.RawBody;
        Log = result.Trace;
        return Page();
    }

    // Invia il messaggio (POST /messages/{cf})
    public async Task<IActionResult> OnPostInviaAsync()
    {
        Service = await _appIo.GetServiceInfoAsync();
        if (!ModelState.IsValid || !TryValidateFiscalCode())
            return Page();

        var result = await _appIo.SendMessageAsync(Input.FiscalCode, Input.Subject, Input.Markdown);
        Ok = result.Success;
        StatusText = result.Success
            ? $"Messaggio inviato. ID: {result.MessageId}"
            : $"Invio fallito (HTTP {result.StatusCode}).";
        Details = result.RawBody;
        Log = result.Trace;
        return Page();
    }

    private bool TryValidateFiscalCode()
    {
        if (string.IsNullOrWhiteSpace(Input.FiscalCode) || Input.FiscalCode.Trim().Length != 16)
        {
            ModelState.AddModelError("Input.FiscalCode", "Il codice fiscale deve avere 16 caratteri.");
            return false;
        }
        Input.FiscalCode = Input.FiscalCode.Trim().ToUpperInvariant();
        return true;
    }

    public class MessageInput
    {
        [Required(ErrorMessage = "Codice fiscale obbligatorio.")]
        [StringLength(16, MinimumLength = 16, ErrorMessage = "Il codice fiscale deve avere 16 caratteri.")]
        [Display(Name = "Codice fiscale")]
        public string FiscalCode { get; set; } = "";

        [Required(ErrorMessage = "Oggetto obbligatorio.")]
        [StringLength(120, MinimumLength = 10, ErrorMessage = "L'oggetto deve avere tra 10 e 120 caratteri.")]
        [Display(Name = "Oggetto")]
        public string Subject { get; set; } = "";

        [Required(ErrorMessage = "Testo del messaggio obbligatorio.")]
        [StringLength(10000, MinimumLength = 80, ErrorMessage = "Il testo deve avere almeno 80 caratteri (requisito AppIO).")]
        [Display(Name = "Messaggio (markdown)")]
        public string Markdown { get; set; } = "";
    }
}
