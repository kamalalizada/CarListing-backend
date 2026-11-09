# AI axtarış

`POST /api/Cars/ai-search/parse` — sərbəst cümləni struktur filtrə çevirir.

```
{"query": "2015-dən yuxarı dizel SUV, 30 minə qədər"}
   ↓
{"filter": {"fuelType":"Dizel","bodyType":"SUV","yearMin":2015,"priceMax":30000}, "notes":[]}
```

## Necə işləyir

Sorğu iki mərhələdən keçir:

```
sorğu
  ↓
QueryRuleParser  ── tam anlaşıldı? ──► cavab   (0 ms, 0 ₼)
  ↓ anlaşılmadı
Ollama (lokal)   ──────────────────► cavab   (~3-15 s, pulsuz)
  ↓ xəta / vaxt bitdi / pozuq JSON
qayda nəticəsi   ──────────────────► cavab
```

Ölçdüyümüz nümunə sorğularda **5/7 pulsuz** həll olunur; model yalnız
qaydaların anlamadığı cümlələrə çağırılır.

### Model nə vaxt çağırılır

`RuleParseVerdict.NeedsModel` qərar verir:

- qaydalar **heç nə** tapmayıbsa
- ya da `RuleLeftoverWordLimit`-dən çox söz anlaşılmamış qalıbsa

Limiti artırmaq → daha ucuz, daha az anlayış. Azaltmaq → əksi.

## Qurulma

API açarı **yalnız environment dəyişənindən** oxunur — `appsettings.json`-a
yazılmır, çünki o fayl git-ə düşür.

**Windows (PowerShell), cari sessiya üçün:**

```powershell
$env:GEMINI_API_KEY = "AIza..."
dotnet run
```

**Daimi (istifadəçi üçün):**

```powershell
[Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "AIza...", "User")
```

**Development üçün user-secrets (tövsiyə olunan):**

```bash
dotnet user-secrets set "GEMINI_API_KEY" "AIza..."
```

Açar `console.anthropic.com` → API Keys bölməsindən alınır.

### Açar olmadan

Tətbiq çökmür və axtarış zəifləmir. Hibrid həmişə qeydiyyatdan keçir; açar
yoxdursa sadəcə model mərhələsi əlavə olunmur, qaydalar isə eyni işləyir.
Yerli inkişaf və CI bundan istifadə edir.

Qaydaların anlamadığı cümlə bu halda boş filtrlə qayıdır — modelsiz daha yaxşı
cavab yoxdur, amma sorğu uğursuz olmur.

## Ayarlar

`appsettings.json` → `AiSearch`:

| Açar | Default | Nə edir |
|---|---|---|
| `Enabled` | `true` | `false` — modeli tamamilə söndürür, redeploy lazım deyil |
| `OllamaBaseUrl` | `""` | Boş — model söndürülür. Dev-də `http://localhost:11434` |
| `OllamaModel` | `qwen2.5:7b` | Lokal model adı |
| `TimeoutSeconds` | `12` (dev: `45`) | Bundan gec cavab qayda nəticəsinə düşür |
| `RuleLeftoverWordLimit` | `2` | Neçə anlaşılmamış söz modelə göndərməyə dəyər |

## Xərc

**Sıfır.** Model lokal işləyir: nə API açarı, nə limit, nə hesab.

Əvəzi gecikmədir. Ölçülmüş dəyərlər (qwen2.5:7b, GPU):

| Hal | Vaxt |
|---|---|
| Model RAM-a yüklənir (ilk sorğu) | ~30-60 s |
| Adi sorğu (model yüklü) | 3-15 s |
| Qayda parseri (fallback) | 0-2 ms |

Hibrid sayəsində sadə sorğular modelə çatmır, ona görə istifadəçilərin
çoxu bu gecikməni görmür.

### Model işləmədikdə

Üç hal fallback-ə düşür və ayrıca loglanır:

| Vəziyyət | Log |
|---|---|
| Ollama işləmir | `Ollama is not running at …` |
| Model yüklənməyib | `model 'qwen2.5:7b' is not installed - run: ollama pull …` |
| Cavab gecikdi | `Ollama did not answer within 45s` |
| Pozuq JSON | `Ollama returned malformed JSON` |

Hər halda axtarış **işləməyə davam edir** — qayda parseri cavab verir.

## Fayllar

| Fayl | Rolu |
|---|---|
| `QueryRuleParser` | Pulsuz mərhələ: qaydalarla oxuma |
| `RuleParseVerdict` | Modelə keçid qərarı |
| `HybridAiSearchParserService` | İkisini birləşdirir |
| `OllamaAiSearchParserService` | Lokal model çağırışı, ingiliscə prompt, JSON təmizləmə |
| `GeminiAiSearchParserService` | Alternativ: hostlanmış model (açar varsa) |
| `IModelSearchParser` | Hibridin model təbəqəsi ilə müqaviləsi |
| `AiFilterReader` | Modelin cavabını doğrulayır |
| `DbSearchVocabularyProvider` | İcazəli dəyərləri bazadan oxuyur |

### Niyə lüğət bazadan oxunur

`GET /Cars/metadata` bir vaxtlar `"Diesel"` və `"Automatic"` qaytarırdı, baza
isə `"Dizel"` və `"Avtomat"` saxlayır — belə filtr heç nə tapmır və istifadəçi
bunu "maşın yoxdur" kimi oxuyur. İndi həm qaydalar, həm model yalnız elanlarda
**həqiqətən mövcud olan** dəyərləri işlədir.

## Təhlükəsizlik

Modelin cavabı **etibarsız giriş** kimi qəbul olunur. `AiFilterReader`:

- lüğətdə olmayan dəyəri atır və istifadəçiyə qeyd yazır
- 7B modelin markdown blokunu/izahat cümləsini təmizləyir (`ExtractJson`)
- ağlabatmayan il və qiyməti rədd edir
- tərs aralığı (`priceMin > priceMax`) düzəldir
- sıralama sahəsini icazəli siyahı ilə yoxlayır
