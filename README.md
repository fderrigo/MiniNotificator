# MiniNotificator

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)

Strumento web minimale, a pagina singola, per inviare un messaggio a un cittadino
tramite le API di **AppIO** (la piattaforma di notifiche pubbliche italiana) e per
verificare preventivamente che il destinatario abbia abilitato il servizio.

Pensato come interfaccia autonoma utile per test e verifiche manuali.

---

## Funzionalità

- **Verifica destinatario** — `POST /profiles`: controlla se il codice fiscale ha
  abilitato il servizio (campo `sender_allowed`).
- **Invio messaggio** — `POST /messages/{codiceFiscale}`: invia oggetto + corpo in
  markdown e restituisce l'ID del messaggio creato.
- **Banner servizio** — tramite la *Manage key* legge e mostra ente, nome servizio
  e ambiente per conto del quale si sta inviando (API *Manage* / Get User Services).
- **Traccia diagnostica** — ogni chiamata produce un log leggibile (metodo, URL,
  esito HTTP, interpretazione dell'autenticazione). La subscription key viene
  **sempre mascherata**, mai mostrata in chiaro.

L'autenticazione verso AppIO avviene con l'header `Ocp-Apim-Subscription-Key`.

## Stack tecnologico

- ASP.NET Core **Razor Pages**, **.NET 9**
- `HttpClient` tipizzato (`AppIoClient`) registrato via DI
- Opzioni fortemente tipizzate (`AppIoOptions`) bind dalla sezione `AppIo`
- Nessun database, nessuna autenticazione utente: solo verifica e invio

## Requisiti

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) o superiore
- Una **subscription key** AppIO (ambiente di test o produzione)
- (Opzionale) una **Manage key** AppIO per il banner informativo del servizio

## Configurazione

Le chiavi reali **non sono versionate**: il repository contiene solo
`appsettings.template.json`. Per configurare l'applicazione, copia il template e
inserisci le tue chiavi:

```powershell
Copy-Item MiniNotificator/appsettings.template.json MiniNotificator/appsettings.json
```

Poi compila la sezione `AppIo` in `MiniNotificator/appsettings.json`:

```json
"AppIo": {
  "AppKey": "<la-tua-subscription-key>",
  "Endpoint": "https://api.io.italia.it/api/v1",
  "ManageEndpoint": "https://api.io.pagopa.it/api/v1/manage",
  "ManageKey": "<la-tua-manage-key-opzionale>"
}
```

> ⚠️ **Sicurezza** — `appsettings.json`, `appsettings.Development.json` e i file
> `*.secrets.json` sono esclusi da Git (vedi `.gitignore`). **Non committare mai
> chiavi reali.** In sviluppo è preferibile usare i
> [User Secrets di .NET](https://learn.microsoft.com/aspnet/core/security/app-secrets)
> o variabili d'ambiente. Usa le chiavi dell'ambiente di **test** finché non sei
> in produzione.

## Avvio

```powershell
dotnet run --project MiniNotificator
```

Apri quindi l'URL indicato in console (es. `https://localhost:7245`).

## Vincoli imposti da AppIO

| Campo            | Vincolo                          |
|------------------|----------------------------------|
| Codice fiscale   | esattamente 16 caratteri         |
| Oggetto          | da 10 a 120 caratteri            |
| Testo (markdown) | minimo 80 caratteri              |

## Struttura del progetto

```
MiniNotificator/
├─ Program.cs                     # bootstrap, DI, pipeline
├─ Services/
│  ├─ AppIoClient.cs             # client HTTP verso le API AppIO + Manage
│  └─ AppIoOptions.cs            # opzioni di configurazione (sezione "AppIo")
├─ Pages/
│  ├─ Index.cshtml(.cs)          # interfaccia: verifica e invio
│  └─ ...
└─ appsettings.template.json     # template di configurazione (senza segreti)
```

## Provenienza e marchi

Le API e il servizio *IO* / *AppIO* sono di **PagoPA S.p.A.**; *IO* e *PagoPA*
sono marchi dei rispettivi titolari. Questo progetto **non è affiliato né
sponsorizzato** da PagoPA S.p.A. Vedi il file [`NOTICE`](./NOTICE).

## Licenza

Distribuito con licenza **Apache 2.0**. Vedi i file [`LICENSE`](./LICENSE) e
[`NOTICE`](./NOTICE).

Copyright © 2026 Filippo D'Errigo.
